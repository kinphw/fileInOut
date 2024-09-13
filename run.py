from fileInOut import FileIO

flag = input("1. 작업 2. 복구>>")
if flag == '1':
    FileIO(True).run()
elif flag == '2':
    FileIO(False).run()
else:    
    pass