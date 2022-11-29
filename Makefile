EXE=linstor-wmi-helper.exe
# default is from my Linux host - not the container
# container must set this:
WINEPREFIX ?= /home/johannes/.wine-32bit

all: $(EXE)

clean:
	rm -rf $(EXE)

%.exe: %.cs
ifeq ($(shell uname -o),Cygwin)
	./run-csc.bat $<
else
# must install wine via apt and Visual Studio 2022 from .tar.gz
# and wine-mono (not the one from Ubuntu)
	WINEPREFIX=$(WINEPREFIX) wine '$(WINEPREFIX)/drive_c/Program Files/Microsoft Visual Studio/2022/Community/Msbuild/Current/Bin/Roslyn/csc.exe' $<
endif
