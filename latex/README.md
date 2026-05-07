# LaTeX Template

Template basico para escrever documentos em LaTeX usando TeX Live e a extensao LaTeX Workshop no VS Code.

## Estrutura

- main.tex: documento principal
- references.bib: bibliografia de exemplo
- .latexmkrc: centraliza os arquivos de build em build/
- .vscode/settings.json: configuracao do build

## Como usar

1. Abra a pasta latex-template no VS Code.
2. Edite o arquivo main.tex.
3. Rode o comando LaTeX Workshop: Build LaTeX project.
4. Use o preview do PDF pela extensao.

## Artefatos

Todos os arquivos gerados pela compilacao ficam em build/, incluindo PDF, arquivos auxiliares, logs e metadados do BibTeX.

## Requisitos

- TeX Live instalado
- latexmk disponivel no sistema
- extensao LaTeX Workshop instalada no VS Code

## Instalando TeX Live

### Arch Linux

Como recomendação, instale o texlive-meta para obter uma instalação completa do TeX Live:

```bash
sudo pacman -S texlive-meta
```

### Windows

Não sei

### Ios

Não sei
