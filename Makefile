EXE=linstor-wmi-helper.exe

all: $(EXE)

clean:
	rm -rf $(EXE)

%.exe: %.cs
ifeq ($(shell uname -o),Cygwin)
	./run-csc.bat $<
else
# must install wine via apt and Visual Studio 2022 from .tar.gz
	wine '/home/johannes/.wine/drive_c/Program Files/Microsoft Visual Studio/2022/Community/Msbuild/Current/Bin/Roslyn/csc.exe' $<
endif
