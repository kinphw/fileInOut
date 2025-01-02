from fileInOut.fileIO import FileIO
from fileInOut.fileIOClipboard import FileIOClipboard

def print_menu():
    print("=== Base64 인코딩/디코딩 프로그램 ===")
    print("1. 파일 처리")
    print("2. 클립보드 처리")
    print("선택: ", end="")

def print_submenu():
    print("\n=== 작업 선택 ===")
    print("1. 인코딩")
    print("2. 디코딩")
    print("선택: ", end="")

# 메인 메뉴 선택
print_menu()
mode = input()

if mode not in ['1', '2']:
    print("잘못된 선택입니다.")
    exit()

# 서브 메뉴 선택
print_submenu()
operation = input()

if operation not in ['1', '2']:
    print("잘못된 선택입니다.")
    exit()

# 파일 모드
if mode == '1':
    if operation == '1':
        FileIO(True).run()
    else:
        FileIO(False).run()
# 클립보드 모드
else:
    clipboard_io = FileIOClipboard()
    if operation == '1':
        clipboard_io.encode_file_to_clipboard()
    else:
        clipboard_io.decode_clipboard_to_file()