import base64

from lib import myFileDialog as myfd

class FileIO:

    strWork = "" #작업 or 복구
    flag:bool

    def __init__(self, flag:bool):
        self.flag = flag
        if flag == True:
            self.strWork = "작업"
        elif flag == False:
            self.strWork = "복구"        
    
    def run(self):
        filename_input, tmp_bytes = self.read()
        tmp_bytes = self.handle(tmp_bytes)
        self.write(filename_input, tmp_bytes)

    def read(self) -> tuple[str, bytes]:
        filename_input = myfd.askopenfilename(self.strWork + "대상파일")

        tmp_file = open(filename_input, 'rb')
        tmp_bytes = tmp_file.read()
        tmp_file.close()

        return filename_input, tmp_bytes

    def handle(self, tmp_bytes:bytes) -> bytes:        
        if self.flag == True:
            tmp_bytes = base64.b64encode(tmp_bytes)
        elif self.flag == False:
            tmp_bytes = base64.b64decode(tmp_bytes)            
        return tmp_bytes

    def write(self, filename_input:str, tmp_bytes:bytes) -> None:
        filename_output = filename_input.rsplit('.', 1)[0] + f'_{self.strWork}.' + filename_input.rsplit('.', 1)[1]
        tmp_file = open(filename_output, 'wb')
        tmp_file.write(tmp_bytes)
        tmp_file.close()
        print(f"{self.strWork} 파일 생성 완료.."+filename_output)
