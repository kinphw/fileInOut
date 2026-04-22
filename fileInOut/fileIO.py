import base64
import os
from lib import myFileDialog as myfd

class FileIO:
    strWork = ""  # 작업 or 복구
    flag: bool

    def __init__(self, flag: bool):
        self.flag = flag
        if flag:
            self.strWork = "작업"
        else:
            self.strWork = "복구"

    def run(self):
        filename_input = myfd.askopenfilename(self.strWork + "대상파일")
        self.process_file(filename_input)

    def process_file(self, filename_input: str) -> None:
        filename_output = filename_input.rsplit('.', 1)[0] + f'_{self.strWork}.' + filename_input.rsplit('.', 1)[1]

        with open(filename_input, 'rb') as f:
            data = f.read()

        result = base64.b64encode(data) if self.flag else base64.b64decode(data)

        with open(filename_output, 'wb') as f:
            f.write(result)

        print(f"{self.strWork} 파일 생성 완료.. " + filename_output)
