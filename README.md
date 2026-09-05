# Dofus 2.6 Local (offline)

Servidor **local** de Dofus 2.6.2 para jogar sozinho no PC (sem internet no jogo).

## Para o seu amigo (passo a passo)

1. Abra a página de **Releases**: https://github.com/CristiancMartini/dofus-2.6/releases
2. Baixe o arquivo **`DofusLocalSetup.exe`**
3. Dê dois cliques e aceite o aviso do Windows / Administrador
4. Clique em **Instalar e deixar pronto** e espere (baixa ~1,6 GB na primeira vez)
5. Na Área de Trabalho, use o atalho **Jogar Dofus Local**
6. No jogo, entre com:
   - **Login:** `test`
   - **Senha:** `test`

### Requisitos
- Windows 10 ou 11
- ~3 GB livres no disco
- Internet só na instalação (depois joga offline)
- Adobe AIR (o instalador tenta instalar sozinho)

### Se o Windows bloquear o .exe
Clique em **Mais informações** → **Executar assim mesmo**.

### Parar o servidor
Rode `PARAR_DOFUS_LOCAL.cmd` na pasta  
`%LOCALAPPDATA%\DofusLocal`

---

## Instalação offline (sem download automático)

Baixe da mesma Release e coloque **na mesma pasta** do `DofusLocalSetup.exe`:

- `DofusLocal-Runtime.zip`
- `Dofus-Client.zip`

Depois rode o instalador (ele usa os arquivos locais).

## Contas
| Login | Senha |
|-------|-------|
| test  | test  |
| admin | test  |

Só funciona em `127.0.0.1` (neste computador).
