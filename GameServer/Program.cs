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
    private const int Port = 5020;
    private const int MaxPacketSize = 65536; // 64KB 제한 (원하는 크기로 조정 가능)

    static async Task Main(string[] args)
    {
        var listener = new TcpListener(IPAddress.Any, Port);
        listener.Start(100); // backlog 제한
        Console.WriteLine($"[GameServer] Listening on port {Port}...");

        while (true)
        {
            var client = await listener.AcceptTcpClientAsync();
            Console.WriteLine("[GameServer] Client accepted");

            // Task.Run으로 실행하여 CPU 폭주 방지
            _ = Task.Run(() => HandleClientAsync(client));
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
                if (read == 0) break; // 정상 종료
                if (read < 4)
                {
                    Console.WriteLine("[GameServer] Invalid header received");
                    break;
                }

                int length = BinaryPrimitives.ReadInt32LittleEndian(lengthBuffer);
                if (length <= 0 || length > MaxPacketSize)
                {
                    Console.WriteLine($"[GameServer] Invalid packet length: {length}");
                    break;
                }

                // 2. 본문 읽기
                byte[] buffer = new byte[length];
                int offset = 0;
                while (offset < length)
                {
                    int r = await stream.ReadAsync(buffer, offset, length - offset);
                    if (r <= 0)
                    {
                        Console.WriteLine("[GameServer] Client disconnected unexpectedly");
                        return;
                    }
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
                else
                {
                    Console.WriteLine($"[GameServer] Unknown packet type: {envelope.Type}");
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