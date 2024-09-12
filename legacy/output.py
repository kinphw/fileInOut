import base64
import win32clipboard as w32
fo=input("복원해낼 파일명과 확장자 입력해라 KILL TH:")
w32.OpenClipboard()
fout=w32.GetClipboardData()
w32.CloseClipboard()

fout2=fout.encode('utf-8')

fout3=base64.b64decode(fout2)

b=open(fo,'wb')
b.write(fout3)
b.close()
