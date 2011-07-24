
CSC = gmcs -debug

TARGET_NAME = Latex
TARGET = $(TARGET_NAME).dll

CSFLAGS = -target:library -resource:Latex.addin.xml

CSFILES = $(srcdir)/Latex.cs \
	  $(srcdir)/LatexPreferences.cs \
	  $(srcdir)/LatexPreferencesFactory.cs
	
ASSEMBLIES = -pkg:tomboy-addins -pkg:gtk-sharp-2.0 -r:Mono.Posix

$(TARGET): $(CSFILES)
	$(CSC) -out:$@ $(CSFLAGS) $(ASSEMBLIES) $^

all: $(TARGET)

install-data-local: $(TARGET)
	$(mkinstalldirs) $(DESTDIR)$(TOMBOY_ADDINSDIR)
	$(INSTALL_DATA) $(TARGET) $(DESTDIR)$(TOMBOY_ADDINSDIR)

uninstall-local:
	rm -f $(TOMBOY_ADDINS_DIR)/$(TARGET)


EXTRA_DIST =			\
	$(CSFILES)		\
	$(srcdir)/Latex.addin.xml


CLEANFILES =			\
	$(TARGET)		\
	$(TARGET).mdb
