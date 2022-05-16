EXE=linstor-helper.exe

all: $(EXE)

clean:
	rm -rf $(EXE)

%.exe: %.cs
	./run-csc.bat $<
	
