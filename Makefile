all: linstor-helper.exe

%.exe: %.cs
	./run-csc.bat $<
	
