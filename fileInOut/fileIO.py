import base64
import os
from tqdm import tqdm  # ProgressBar를 위한 라이브러리
from lib import myFileDialog as myfd

class FileIO:
    strWork = ""  # 작업 or 복구
    flag: bool
    chunk_size = 10 * 1024 * 1024  # 10MB로 분할

    def __init__(self, flag: bool):
        self.flag = flag
        if flag:
            self.strWork = "작업"
        else:
            self.strWork = "복구"

    def run(self):
        filename_input, file_size = self.read()
        self.process_file(filename_input, file_size)

    def read(self) -> tuple[str, int]:
        filename_input = myfd.askopenfilename(self.strWork + "대상파일")
        file_size = os.path.getsize(filename_input)  # 파일 크기 확인
        return filename_input, file_size

    def process_file(self, filename_input: str, file_size: int) -> None:
        filename_output = filename_input.rsplit('.', 1)[0] + f'_{self.strWork}.' + filename_input.rsplit('.', 1)[1]
        
        # ProgressBar 초기화 (파일 사이즈를 기준으로 분할 크기 계산)
        with tqdm(total=file_size, unit='B', unit_scale=True, desc=self.strWork + " 진행중") as pbar:
            with open(filename_input, 'rb') as f_input, open(filename_output, 'wb') as f_output:
                while True:
                    chunk = f_input.read(self.chunk_size)
                    if not chunk:
                        break

                    processed_chunk = self.handle(chunk)

                    f_output.write(processed_chunk)
                    pbar.update(len(chunk))  # ProgressBar 업데이트

        print(f"{self.strWork} 파일 생성 완료.. " + filename_output)

    def handle(self, chunk: bytes) -> bytes:
        if self.flag:
            # 인코딩 처리
            chunk = base64.b64encode(chunk)
        else:
            # 디코딩 처리
            chunk = base64.b64decode(chunk)
        return chunk
