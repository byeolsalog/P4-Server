using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Google.Protobuf;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Buffers.Binary; // BinaryPrimitives 사용
using Netproto;

class Program
{
    static async Task Main(string[] args)
    {
        var listener = new TcpListener(IPAddress.Any, 5020);
        listener.Start();
        Console.WriteLine("[GameServer] Listening on port 5020...");

        while (true)
        {
            var client = await listener.AcceptTcpClientAsync();
            _ = HandleClientAsync(client); // fire & forget
        }
    }

    private static async Task HandleClientAsync(TcpClient client)
    {
        Console.WriteLine("[GameServer] Client connected");
        using var stream = client.GetStream();

        try
        {
            while (true)
            {
                // 1. 길이 프리픽스(4바이트) 읽기
                byte[] lengthBuffer = new byte[4];
                int read = await stream.ReadAsync(lengthBuffer, 0, 4);
                if (read == 0) break;

                int length = BinaryPrimitives.ReadInt32LittleEndian(lengthBuffer);

                // 2. 본문 읽기
                byte[] buffer = new byte[length];
                int offset = 0;
                while (offset < length)
                {
                    int r = await stream.ReadAsync(buffer, offset, length - offset);
                    if (r <= 0) throw new Exception("클라이언트 연결 끊김");
                    offset += r;
                }

                // 3. Envelope 파싱
                var envelope = Envelope.Parser.ParseFrom(buffer);
                Console.WriteLine($"[GameServer] Received {envelope.Type}");

                if (envelope.Type == nameof(LoginReq))
                {
                    var loginReq = LoginReq.Parser.ParseFrom(envelope.Payload);
                    var loginRes = HandleLogin(loginReq);

                    // 4. 응답 포장
                    var resEnvelope = new Envelope
                    {
                        ReqId = envelope.ReqId,
                        Type = nameof(LoginRes),
                        Payload = loginRes.ToByteString()
                    };

                    using var ms = new MemoryStream();
                    resEnvelope.WriteTo(ms);
                    byte[] data = ms.ToArray();

                    // BinaryPrimitives로 길이 기록
                    byte[] lengthPrefix = new byte[4];
                    BinaryPrimitives.WriteInt32LittleEndian(lengthPrefix, data.Length);

                    await stream.WriteAsync(lengthPrefix, 0, 4);
                    await stream.WriteAsync(data, 0, data.Length);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GameServer] Error: {ex.Message}");
        }
        finally
        {
            client.Close();
            Console.WriteLine("[GameServer] Client disconnected");
        }
    }

    private static LoginRes HandleLogin(LoginReq req)
    {
        try
        {
            // JWT 검증
            var handler = new JwtSecurityTokenHandler();
            var jwtKey = "POZvoReoCqeIEypIJisctMpSrJcwrVRJGMq/RR4HvvanJ5OY1jwkMSlK/LPnAKj7L6Co7fBSAMXnpjzjQ87C5w==";
            var parameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = "game-login",
                ValidateAudience = true,
                ValidAudience = "game-server",
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
            };

            var principal = handler.ValidateToken(req.AccessJwt, parameters, out _);

            var userId = principal.FindFirst("userId")?.Value ?? "0";
            var displayName = principal.FindFirst("displayName")?.Value ?? "Unknown";
            var createdAt = DateTime.UtcNow.AddHours(9).ToString("yyyy-MM-dd HH:mm:ss");
            var lastLogin = createdAt;

            Console.WriteLine($"[GameServer] 로그인 성공 UserId={userId}, Name={displayName}");

            return new LoginRes
            {
                Success = true,
                Message = "Login success",
                UserId = int.Parse(userId),
                DisplayName = displayName,
                CreatedAt = createdAt,
                LastLogin = lastLogin
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GameServer] 로그인 실패: {ex.Message}");
            return new LoginRes
            {
                Success = false,
                Message = "Invalid token",
                UserId = 0,
                DisplayName = "",
                CreatedAt = "",
                LastLogin = ""
            };
        }
    }
}