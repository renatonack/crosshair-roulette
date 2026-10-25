# Crosshair Roulette

A cada abate no CS2, a mira muda para uma mira aleatória, igual ao vídeo "CS2 crosshair roulette".
As miras são as do próprio vídeo: linhas que atravessam a tela, quadrados vazados, "U" vermelho,
bloco amarelo, jogo da velha azul, cruz azul gigante e a tela inteira pintada (você só enxerga quando atira).

## Baixar

👉 **[Baixar o CrosshairRoulette.exe](../../releases/latest)**: é um único arquivo de ~93 KB.
Não precisa instalar nada, só ter Windows 10 ou 11.

**Na primeira vez:**
- O Windows pode mostrar "O Windows protegeu o computador". Isso acontece porque o programa não tem
  assinatura digital paga. Clique em **Mais informações → Executar assim mesmo**.
- Algum antivírus pode desconfiar de um .exe novo e pequeno que desenha por cima de outras janelas.
  É alarme falso; o código inteiro está aqui no repositório para quem quiser conferir.

## Como usar

1. Abra o **CrosshairRoulette.exe**. Para ter atalho na área de trabalho e no menu Iniciar, clique em
   "Criar atalhos" na janela.
2. O programa encontra o CS2 sozinho e instala a integração (veja "Conexão com o CS2" na janela).
   Se o CS2 estava aberto, reinicie o jogo uma vez.
3. No CS2: **Vídeo → Modo de exibição → Tela cheia em janela**. Em tela cheia exclusiva nenhuma mira
   externa aparece (o mesmo vale para o Crosshair V2).
   **Joga esticado (4:3, ex. 1440×1080)?** Em "Tela cheia em janela" o CS2 usa a resolução do Windows.
   Coloque o Windows em 1440×1080 e, no painel da placa de vídeo, ative a escala "Tela cheia / Esticar".
4. Esconda a mira do jogo: no console digite `crosshair 0` (para voltar: `crosshair 1`).
   A janela tem botões para copiar os dois comandos.
5. Faça um abate.

Fechar a janela deixa o programa rodando na bandeja. Para sair, clique com o botão direito no ícone.

## Dois modos

- **Roleta**: a cada abate a mira muda para uma mira aleatória (as do vídeo).
- **Mira fixa (estilo Crosshair V2)**: você escolhe uma mira e ela fica sempre na tela. Os abates não mudam nada.
  Dá para escolher o formato (cruz, cruz + ponto, ponto, T, círculo, círculo + ponto), a cor
  (inclusive **arco-íris**, que fica trocando de cor) e ajustar tamanho, espessura, espaço no meio e contorno preto.

Troque de modo no topo da janela ou pelo ícone da bandeja.

## Atalhos

- `F7`: sortear mira agora
- `F8`: mostrar / esconder a mira

Os atalhos podem ser trocados na janela.

## Na janela

- Clique numa mira para usar ela agora.
- Desmarque uma mira para tirar ela da roleta.
- "Incluir miras aleatórias geradas" também sorteia miras novas no estilo do CS2.
- Também dá para ajustar tamanho, opacidade, monitor e som, e ligar "Iniciar com o Windows".

## É seguro?

Sim. Os abates chegam pela **Game State Integration**, recurso oficial da Valve usado por HUDs e
programas de stream. O programa não lê a memória do jogo nem mexe em nada além de um arquivo `.cfg`.
A mira é só uma janela transparente por cima do jogo.

---

## Desenvolvimento

- `nativo/src/*.cs`: o programa (C# / WinForms, .NET Framework 4.8)
- `nativo/build.ps1`: compila `nativo/dist/CrosshairRoulette.exe` com o compilador C# do Visual Studio 2022 Build Tools
- `nativo/test/snapshot.ps1`: abre a janela fora da tela com configurações de teste e salva fotos dos dois modos
- `tools/fake-cs2.js`: simula o CS2 (`node tools/fake-cs2.js kill|hs|shoot|die`)

As configurações ficam em `%APPDATA%\Crosshair Roulette\settings.json`.
