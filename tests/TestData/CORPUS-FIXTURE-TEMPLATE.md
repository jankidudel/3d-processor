# Corpus fixture manifest template

Create one adjacent Markdown file for each external corpus fixture. For
example, `sample.3mf` has `sample.3mf.md`.

```md
# <fixture file name>

- Format: <STL | 3MF | ...>
- Purpose: <what product behaviour this exercises>
- Source: <direct, stable download or source-page URL>
- Source version / download date: <version or YYYY-MM-DD>
- Licence: <licence name and URL>
- SHA-256: `<lowercase hexadecimal digest>`
- Producing tool and version: <tool/version, or "unknown">
- Original file name: <name at the source, if different>
- Local changes: <none, or exact reproducible changes made>
- Size: <bytes>
```

Never put credentials, customer data, or personally identifying metadata in a
fixture. If the source licence does not allow redistribution, do not commit the
file; store only its manifest and fetch it in a separately authorised test
workflow.
