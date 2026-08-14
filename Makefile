SHELL := /bin/sh

PROJECT := src/Processor/Processor.csproj
PROJECT_DIR := src/Processor
SOLUTION := 3d-processor.slnx
FRAMEWORK := net10.0
CONFIGURATION := Release
ARTIFACT_DIR := $(CURDIR)/artifacts
PACKAGE := $(ARTIFACT_DIR)/3d-processor.zip

# Global .NET tools are installed here by default. Appending the directory makes
# the Makefile work even when a newly installed dotnet-lambda is not yet on PATH.
export PATH := $(PATH):$(HOME)/.dotnet/tools

.PHONY: build test package clean

build:
	dotnet build "$(PROJECT)" --configuration "$(CONFIGURATION)" --framework "$(FRAMEWORK)" --nologo

test:
	dotnet test "$(SOLUTION)" --nologo

package:
	mkdir -p "$(ARTIFACT_DIR)"
	rm -f "$(PACKAGE)"
	dotnet lambda package \
		--project-location "$(PROJECT_DIR)" \
		--configuration "$(CONFIGURATION)" \
		--framework "$(FRAMEWORK)" \
		--function-architecture x86_64 \
		--output-package "$(PACKAGE)"

clean:
	dotnet clean "$(SOLUTION)" --configuration Debug --nologo
	dotnet clean "$(SOLUTION)" --configuration Release --nologo
	rm -rf "$(ARTIFACT_DIR)"
