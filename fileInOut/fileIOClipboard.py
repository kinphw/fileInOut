import base64
import os
import win32clipboard as w32
from tqdm import tqdm
from lib import myFileDialog as myfd

class FileIOClipboard:
    chunk_size = 10 * 1024 * 1024  # 10MB로 분할

    def __init__(self):
        pass

    def encode_file_to_clipboard(self) -> None:
        filename_input = myfd.askopenfilename("작업 대상파일")
        file_size = os.path.getsize(filename_input)

        # ProgressBar 초기화
        with tqdm(total=file_size, unit='B', unit_scale=True, desc="작업 진행중") as pbar:
            with open(filename_input, 'rb') as f_input:
                # 파일 전체 읽기
                file_content = f_input.read()
                pbar.update(file_size)

                # base64 인코딩
                encoded_content = base64.b64encode(file_content).decode('utf-8')

                # 클립보드에 복사
                w32.OpenClipboard()
                w32.EmptyClipboard()
                w32.SetClipboardText(encoded_content)
                w32.CloseClipboard()

        print(f"파일 내용이 클립보드에 복사되었습니다.")

    def decode_clipboard_to_file(self) -> None:
        # 저장할 파일 이름 받기
        filename_output = myfd.asksaveasfilename("저장할 파일")

        # 클립보드에서 데이터 가져오기
        w32.OpenClipboard()
        clipboard_data = w32.GetClipboardData()
        w32.CloseClipboard()

        # base64 디코딩
        try:
            decoded_data = base64.b64decode(clipboard_data.encode('utf-8'))
            
            # 파일로 저장
            with open(filename_output, 'wb') as f_output:
                f_output.write(decoded_data)
            
            print(f"파일이 성공적으로 저장되었습니다: {filename_output}")
        except Exception as e:
            print(f"디코딩 중 오류가 발생했습니다: {str(e)}")
