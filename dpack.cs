// dpack.cs — 개인 암호 기반 파일 암호화/복호화 도구 (단일 소스, 의존성 없음)
//
// 빌드:  .\publish.ps1  로 self-contained 단일 exe(dpack.exe) 생성 → 폐쇄망 반입.
//        (개발 빌드: dotnet build -c Release)
//
// 사용법:
//     dpack enc <입력파일> [출력파일] [-p 암호] [-c 레벨]   // 암호화(Brotli압축→AES→HMAC)
//     dpack dec <입력파일> [출력파일] [-p 암호]            // 복호화
//   -c 레벨: none|fast|normal(기본)|max  (max=최소용량이지만 매우 느림)
//   -p 를 생략하면 실행 중 암호를 물어봅니다(입력 숨김).
//   출력파일 생략 시  enc: 입력+".dpk" ,  dec: ".dpk" 제거(또는 입력+".out").
//
// 컨테이너 포맷(모두 바이너리):
//     MAGIC "FENC"(4) | ver(1) | flags(1) | iterations(4, BE) | salt(16) | iv(16) | ciphertext(n) | mac(32)
//   키유도 : PBKDF2-HMAC-SHA256(암호, salt, iterations) -> 64바이트 -> 앞32=암호키, 뒤32=MAC키
//   암호화 : (선택 압축)Deflate -> AES-256-CBC(PKCS7, 랜덤 IV)
//   무결성 : HMAC-SHA256(MAC키, header+ciphertext)  [Encrypt-then-MAC, 복호화 전에 검증]

using System;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

static class DPack
{
    const uint DefaultIterations = 600000;               // PBKDF2 반복 횟수
    static readonly byte[] Magic = { 0x46, 0x45, 0x4E, 0x43 }; // "FENC"
    const byte Version = 1;
    // flags 바이트 = 압축 방식
    const byte CompStored  = 0;   // 무압축(원본 유지)
    const byte CompDeflate = 1;   // 예전 파일 복호화 호환용(읽기 전용)
    const byte CompBrotli  = 2;   // 기본 압축
    const int HeaderLen = 4 + 1 + 1 + 4 + 16 + 16;       // = 42
    const int MacLen = 32;

    static int Main(string[] args)
    {
        try
        {
            if (args.Length >= 1)
            {
                string a0 = args[0].ToLowerInvariant();
                if (a0 == "version" || a0 == "-v" || a0 == "--version")
                {
                    Console.WriteLine("dpack " + AppVersion());
                    return 0;
                }
            }
            if (args.Length < 2) { PrintUsage(); return 1; }

            string mode = args[0].ToLowerInvariant();
            string input = args[1];
            string output = null;
            string password = null;
            string level = "normal";   // 압축 레벨: none|fast|normal|max

            for (int i = 2; i < args.Length; i++)
            {
                if (args[i] == "-p" || args[i] == "--pass")
                {
                    if (i + 1 >= args.Length) { Console.Error.WriteLine("암호 값이 없습니다."); return 1; }
                    password = args[++i];
                }
                else if (args[i] == "-c" || args[i] == "--level")
                {
                    if (i + 1 >= args.Length) { Console.Error.WriteLine("압축 레벨 값이 없습니다."); return 1; }
                    level = args[++i].ToLowerInvariant();
                }
                else if (output == null) { output = args[i]; }
                else { Console.Error.WriteLine("알 수 없는 인자: " + args[i]); return 1; }
            }

            if (mode != "enc" && mode != "dec") { PrintUsage(); return 1; }
            if (level != "none" && level != "fast" && level != "normal" && level != "max")
            { Console.Error.WriteLine("압축 레벨은 none|fast|normal|max 중 하나여야 합니다."); return 1; }
            if (!File.Exists(input)) { Console.Error.WriteLine("입력 파일이 없습니다: " + input); return 1; }
            if (password == null) password = ReadPassword("암호 입력: ");
            if (string.IsNullOrEmpty(password)) { Console.Error.WriteLine("암호가 비어 있습니다."); return 1; }

            if (mode == "enc")
            {
                if (output == null) output = input + ".dpk";
                Encrypt(input, output, password, level);
                long si = new FileInfo(input).Length, so = new FileInfo(output).Length;
                Console.WriteLine("암호화 완료 -> " + output);
                Console.WriteLine(string.Format("  {0:N0} -> {1:N0} 바이트 ({2:0.0}%)", si, so, si == 0 ? 0 : so * 100.0 / si));
            }
            else
            {
                if (output == null) output = DefaultDecOutput(input);
                Decrypt(input, output, password);
                Console.WriteLine("복호화 완료 -> " + output);
            }
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("오류: " + ex.Message);
            return 2;
        }
    }

    static void Encrypt(string inPath, string outPath, string password, string level)
    {
        byte[] plain = File.ReadAllBytes(inPath);

        // 압축(Brotli). 이득이 있을 때만 적용 — 이미 압축된 바이너리는 원본 유지.
        byte method = CompStored;
        byte[] payload = plain;
        if (level != "none")
        {
            CompressionLevel cl = level == "fast" ? CompressionLevel.Fastest
                                : level == "max"  ? CompressionLevel.SmallestSize
                                :                    CompressionLevel.Optimal;
            bool doIt = true;
            if (level == "max")
            {
                // q11은 매우 느림 → 압축이 거의 안 되는 데이터는 빠른 탐침으로 걸러 건너뜀
                byte[] probe = BrotliCompress(plain, CompressionLevel.Fastest);
                if (probe.Length >= (long)(plain.Length * 0.97)) doIt = false;
            }
            if (doIt)
            {
                byte[] c = BrotliCompress(plain, cl);
                if (c.Length < plain.Length) { payload = c; method = CompBrotli; }
            }
        }

        byte[] salt = RandomBytes(16);
        byte[] iv = RandomBytes(16);
        uint iters = DefaultIterations;

        byte[] encKey, macKey;
        DeriveKeys(password, salt, iters, out encKey, out macKey);
        try
        {
            byte[] cipher;
            using (Aes aes = Aes.Create())
            {
                aes.KeySize = 256;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                aes.Key = encKey;
                aes.IV = iv;
                using (ICryptoTransform enc = aes.CreateEncryptor())
                    cipher = enc.TransformFinalBlock(payload, 0, payload.Length);
            }

            byte[] header = new byte[HeaderLen];
            int o = 0;
            Buffer.BlockCopy(Magic, 0, header, o, 4); o += 4;
            header[o++] = Version;
            header[o++] = method;
            WriteUInt32BE(header, o, iters); o += 4;
            Buffer.BlockCopy(salt, 0, header, o, 16); o += 16;
            Buffer.BlockCopy(iv, 0, header, o, 16); o += 16;

            byte[] mac;
            using (HMACSHA256 h = new HMACSHA256(macKey))
            {
                h.TransformBlock(header, 0, header.Length, null, 0);
                h.TransformFinalBlock(cipher, 0, cipher.Length);
                mac = h.Hash;
            }

            using (FileStream fs = new FileStream(outPath, FileMode.Create, FileAccess.Write))
            {
                fs.Write(header, 0, header.Length);
                fs.Write(cipher, 0, cipher.Length);
                fs.Write(mac, 0, mac.Length);
            }
        }
        finally
        {
            Array.Clear(encKey, 0, encKey.Length);
            Array.Clear(macKey, 0, macKey.Length);
        }
    }

    static void Decrypt(string inPath, string outPath, string password)
    {
        byte[] all = File.ReadAllBytes(inPath);
        if (all.Length < HeaderLen + MacLen)
            throw new Exception("파일이 너무 작거나 형식이 아닙니다.");
        for (int i = 0; i < 4; i++)
            if (all[i] != Magic[i]) throw new Exception("형식이 올바르지 않습니다 (MAGIC 불일치).");

        int o = 4;
        byte ver = all[o++];
        if (ver != Version) throw new Exception("지원하지 않는 버전: " + ver);
        byte method = all[o++];
        uint iters = ReadUInt32BE(all, o); o += 4;
        byte[] salt = new byte[16]; Buffer.BlockCopy(all, o, salt, 0, 16); o += 16;
        byte[] iv = new byte[16]; Buffer.BlockCopy(all, o, iv, 0, 16); o += 16;

        int cipherLen = all.Length - HeaderLen - MacLen;
        byte[] macGiven = new byte[MacLen];
        Buffer.BlockCopy(all, all.Length - MacLen, macGiven, 0, MacLen);

        byte[] encKey, macKey;
        DeriveKeys(password, salt, iters, out encKey, out macKey);
        try
        {
            byte[] macCalc;
            using (HMACSHA256 h = new HMACSHA256(macKey))
            {
                h.TransformBlock(all, 0, HeaderLen, null, 0);
                h.TransformFinalBlock(all, HeaderLen, cipherLen);
                macCalc = h.Hash;
            }
            if (!ConstantTimeEquals(macGiven, macCalc))
                throw new Exception("인증 실패: 암호가 틀렸거나 파일이 변조되었습니다.");

            byte[] payload;
            using (Aes aes = Aes.Create())
            {
                aes.KeySize = 256;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                aes.Key = encKey;
                aes.IV = iv;
                using (ICryptoTransform dec = aes.CreateDecryptor())
                    payload = dec.TransformFinalBlock(all, HeaderLen, cipherLen);
            }

            byte[] plain = Decompress(payload, method);
            File.WriteAllBytes(outPath, plain);
        }
        finally
        {
            Array.Clear(encKey, 0, encKey.Length);
            Array.Clear(macKey, 0, macKey.Length);
        }
    }

    // ---- 도우미 ----

    static void DeriveKeys(string password, byte[] salt, uint iters, out byte[] encKey, out byte[] macKey)
    {
        using (Rfc2898DeriveBytes kdf = new Rfc2898DeriveBytes(password, salt, (int)iters, HashAlgorithmName.SHA256))
        {
            encKey = kdf.GetBytes(32);
            macKey = kdf.GetBytes(32);
        }
    }

    static byte[] BrotliCompress(byte[] data, CompressionLevel level)
    {
        using (MemoryStream ms = new MemoryStream())
        {
            using (BrotliStream bs = new BrotliStream(ms, level, true))
                bs.Write(data, 0, data.Length);
            return ms.ToArray();
        }
    }

    static byte[] Decompress(byte[] data, byte method)
    {
        if (method == CompStored) return data;
        using (MemoryStream ms = new MemoryStream(data))
        using (Stream ds = method == CompBrotli
                ? (Stream)new BrotliStream(ms, CompressionMode.Decompress)
                : (Stream)new DeflateStream(ms, CompressionMode.Decompress))
        using (MemoryStream outMs = new MemoryStream())
        {
            byte[] buf = new byte[81920];
            int n;
            while ((n = ds.Read(buf, 0, buf.Length)) > 0) outMs.Write(buf, 0, n);
            return outMs.ToArray();
        }
    }

    static byte[] RandomBytes(int n)
    {
        byte[] b = new byte[n];
        using (RandomNumberGenerator rng = RandomNumberGenerator.Create()) rng.GetBytes(b);
        return b;
    }

    static bool ConstantTimeEquals(byte[] a, byte[] b)
    {
        if (a.Length != b.Length) return false;
        int diff = 0;
        for (int i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
        return diff == 0;
    }

    static void WriteUInt32BE(byte[] buf, int offset, uint v)
    {
        buf[offset] = (byte)(v >> 24);
        buf[offset + 1] = (byte)(v >> 16);
        buf[offset + 2] = (byte)(v >> 8);
        buf[offset + 3] = (byte)v;
    }

    static uint ReadUInt32BE(byte[] buf, int offset)
    {
        return ((uint)buf[offset] << 24) | ((uint)buf[offset + 1] << 16)
             | ((uint)buf[offset + 2] << 8) | buf[offset + 3];
    }

    static string DefaultDecOutput(string input)
    {
        if (input.EndsWith(".dpk", StringComparison.OrdinalIgnoreCase))
            return input.Substring(0, input.Length - 4);
        return input + ".out";
    }

    static string ReadPassword(string prompt)
    {
        Console.Write(prompt);
        StringBuilder sb = new StringBuilder();
        while (true)
        {
            ConsoleKeyInfo k = Console.ReadKey(true);
            if (k.Key == ConsoleKey.Enter) { Console.WriteLine(); break; }
            if (k.Key == ConsoleKey.Backspace)
            {
                if (sb.Length > 0) { sb.Length--; Console.Write("\b \b"); }
                continue;
            }
            if (k.KeyChar == '\0') continue;
            sb.Append(k.KeyChar);
            Console.Write('*');
        }
        return sb.ToString();
    }

    static string AppVersion()
    {
        Assembly asm = Assembly.GetExecutingAssembly();
        object[] a = asm.GetCustomAttributes(typeof(AssemblyInformationalVersionAttribute), false);
        if (a.Length > 0) return ((AssemblyInformationalVersionAttribute)a[0]).InformationalVersion;
        Version v = asm.GetName().Version;
        return v != null ? v.ToString() : "0.0.0";
    }

    static void PrintUsage()
    {
        Console.WriteLine("dpack " + AppVersion());
        Console.WriteLine("사용법:");
        Console.WriteLine("  dpack enc <입력파일> [출력파일] [-p 암호] [-c 레벨]   # 암호화");
        Console.WriteLine("  dpack dec <입력파일> [출력파일] [-p 암호]            # 복호화");
        Console.WriteLine("  dpack version                                        # 버전 표시");
        Console.WriteLine("  -c 레벨: none|fast|normal(기본)|max  (max=최소용량, 느림)");
        Console.WriteLine("  -p 생략 시 실행 중 암호를 입력받습니다.");
    }
}
