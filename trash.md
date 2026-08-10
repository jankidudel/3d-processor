3d-processor/
├── src/
│   └── Processor/
│       ├── Program.cs
│       ├── Processor.csproj
│       ├── Models/
│       │   ├── Vector3.cs
│       │   ├── Triangle.cs
│       │   └── Mesh.cs
│       ├── Stl/
│       │   ├── StlReader.cs
│       │   ├── BinaryStlReader.cs
│       │   └── AsciiStlReader.cs
│       └── Validation/
│           ├── StlValidator.cs
│           └── ValidationResult.cs
├── tests/
├── data/
├── .gitignore
└── 3d-processor.sln









3d-processor/
├── src/
│   └── Processor/
│       ├── Program.cs
│       ├── Processor.csproj
│       │
│       ├── Models/
│       │   ├── Mesh.cs
│       │   ├── Triangle.cs
│       │   └── Vector3.cs
│       │
│       ├── Stl/
│       │   ├── StlReader.cs
│       │   └── BinaryStlReader.cs
│       │
│       └── Validation/
│           ├── StlValidator.cs
│           └── ValidationResult.cs
│
├── tests/
│   └── Processor.Tests/
│       ├── Processor.Tests.csproj
│       ├── StlReaderTests.cs
│       └── StlValidatorTests.cs
│
├── samples/
│   ├── valid.stl
│   └── broken.stl
│
├── 3d-processor.sln
├── .editorconfig
├── .gitignore
└── README.md