import base64

filename_input=input("비밀의 파일:")
tmp_file=open(filename_input, 'rb')
tmp_bytes=tmp_file.read()
tmp_file.close()

tmp_bytes=base64.b64decode(tmp_bytes)

filename_output=input("복구할 파일명:")

tmp_file=open(filename_output,'wb')
tmp_file.write(tmp_bytes)
tmp_file.close()

print("복구할 파일 생성 완료."+filename_output)
