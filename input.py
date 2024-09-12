import base64
import win32clipboard as w32
fi=input('복제할 파일명과 확장자를 입력해라 KILL TH: ')
a=open(fi, 'rb')
fin=a.read()
a.close()
fin2=base64.b64encode(fin).decode('utf-8')
w32.OpenClipboard()
w32.EmptyClipboard()
w32.SetClipboardText(fin2)
w32.CloseClipboard()
