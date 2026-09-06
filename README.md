# Dofus 2.6 Local

Emulador offline do **Dofus 2.6.2** para Windows. Roda só neste PC (`127.0.0.1`). Idioma **português**, com monstros, NPCs e zaaps.

## Instalação

1. Vá em [Releases](https://github.com/CristiancMartini/dofus-2.6/releases)
2. Baixe o **`DofusLocalSetup.exe`**
3. Execute e aceite a permissão de administrador
4. Clique em **Instalar e deixar pronto** (na primeira vez baixa ~1,6 GB)
5. Abra o atalho **Jogar Dofus Local** na Área de Trabalho

### Login padrão

| Login | Senha |
|-------|-------|
| `test` | `test` |
| `admin` | `test` |

Rates do servidor: **x3 XP** e **x4 drop**.

## Requisitos

- Windows 10 ou 11
- ~3 GB de espaço livre
- Internet apenas na instalação
- Adobe AIR (o instalador tenta instalar automaticamente)

Se o Windows bloquear o `.exe`: **Mais informações** → **Executar assim mesmo**.

## Uso

- **Jogar:** atalho *Jogar Dofus Local* (abre MariaDB, Auth, World e o cliente)
- **Parar:** `PARAR_DOFUS_LOCAL.cmd` em `%LOCALAPPDATA%\DofusLocal`

## Instalação offline

Se preferir não baixar pelo instalador, coloque estes arquivos **na mesma pasta** do `DofusLocalSetup.exe` e rode o setup:

- `DofusLocal-Runtime.zip`
- `Dofus-Client.zip`

Eles estão na [Release](https://github.com/CristiancMartini/dofus-2.6/releases).
