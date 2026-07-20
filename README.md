# dpack — 개인 암호 기반 파일 암호화/복호화

바이너리 파일을 **내 암호로 암호화해서 반출**하고, 반대쪽에서 **같은 암호로 복호화**해 복원하는 단일 C# 도구입니다.
(기존 base64 방식 대체 — base64는 +33% 커졌지만, 이 도구는 **Brotli 압축 후 암호화**라 텍스트성 파일은 크게 작아집니다. 실측: 28.5MB CSV → 1.76MB(기본) / 1.48MB(`-c max`).)

## 운영 흐름

1. 이 PC에서 `.\publish.ps1` 실행 → **self-contained 단일 `publish\dpack.exe`** 생성 (.NET 설치와 무관하게 실행됨)
2. `dpack.exe` 를 폐쇄망에 반입 → `dpack.exe enc 대상파일 -p 내암호` 로 암호화
3. 나온 `.dpk` 파일을 반출 → 이 PC에서 `dpack.exe dec 대상파일.dpk -p 내암호` 로 복원

> exe와 데이터 모두 바이너리로 통과 가능하고, 폐쇄망에도 .NET 8/9 런타임이 있으므로 self-contained exe 하나만 들고 들어가면 됩니다.

## 빌드

가장 간단: **`build.bat` 더블클릭** — self-contained 단일 exe를 만들고 zip까지 패키징합니다.
```
build.bat                  # → publish\dpack.exe  및  dist\dpack-<버전>.zip
```
- 산출물: `publish\dpack.exe`(단일 exe, ~33MB) + `dist\dpack-<버전>.zip`(exe + README)
- 버전은 루트 **`VERSION`** 파일 하나로 관리합니다(단일 소스). **값만 바꾸고 다시 실행**하면 exe/zip 이름에 자동 반영됩니다.

다른 방법:
```powershell
.\publish.ps1              # build.bat과 동일한 exe 생성(zip 없음)
dotnet build -c Release    # 개발/테스트용(런타임 의존) 빌드
```

## 사용법
```powershell
dpack enc <입력파일> [출력파일] [-p 암호] [-c 레벨]   # 암호화 (Brotli압축 → AES-256 → HMAC)
dpack dec <입력파일> [출력파일] [-p 암호]            # 복호화
dpack version                                        # 버전 표시
```
- `-p` 를 생략하면 실행 중 암호를 물어봅니다(입력 숨김).
- 출력파일 생략 시 — `enc`: `입력.dpk`, `dec`: `.dpk` 제거(없으면 `입력.out`).
- **양쪽에서 반드시 같은 암호**를 사용해야 복원됩니다. 암호는 소스/exe에 저장되지 않습니다.

예:
```powershell
dpack enc secret.csv                 # → secret.csv.dpk (기본 압축)
dpack enc secret.csv -c max          # 최소 용량(느림) — 텍스트에 권장
dpack dec secret.csv.dpk             # → secret.csv 복원
```

### 압축 레벨 `-c` (기본 `normal`)

압축은 이득이 있을 때만 적용되고, 안 줄면 자동으로 원본을 그대로 저장하므로 **어떤 데이터도 커지지 않습니다.**

| 레벨 | 설명 | 28.5MB CSV 실측 | 20MB 랜덤 바이너리 |
|---|---|---|---|
| `none` | 압축 안 함 | 100% | 100% (즉시) |
| `fast` | 빠름 | ~9% | 원본 유지(즉시) |
| `normal` (기본) | 균형 — 빠르고 충분히 작음 | **6.5% / 3.6초** | 원본 유지 / 2.9초 |
| `max` | 최소 용량, 매우 느림 | **5.2% / 142초** | 원본 유지 / 3.3초 |

> `max`는 텍스트/CSV에 유리하지만 매우 느립니다(1GB면 수십 분). **압축이 거의 안 되는 데이터는 빠른 탐침으로 자동 감지해 건너뜁니다** → 바이너리에 `max`를 줘도 시간 낭비가 없습니다. 일상적으로는 `normal`, 반출 용량을 끝까지 짜야 할 때만 `max`를 쓰세요.

## 보안 설계

| 항목 | 방식 |
|---|---|
| 키 유도 | PBKDF2-HMAC-SHA256, 반복 600,000회, 랜덤 salt 16B (오프라인 무차별 대입 방어) |
| 암호화 | AES-256-CBC (PKCS7, 매번 랜덤 IV) |
| 무결성/인증 | HMAC-SHA256 (Encrypt-then-MAC) — 복호화 **전에** 검증, 틀린 암호·변조 시 거부 |
| 압축 | Brotli (`-c` 레벨 선택, 이득일 때만 적용, 이미 압축된 파일은 원본 유지) |

### 컨테이너 포맷 (모두 바이너리)
```
MAGIC(4) | ver(1) | flags(1) | iterations(4, BE) | salt(16) | iv(16) | ciphertext(n) | mac(32)
```

> ⚠️ 암호를 잊으면 **복구 불가능**합니다. 강한 암호를 안전하게 보관하세요.
