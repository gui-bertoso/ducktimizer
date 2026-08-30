# Ducktimizer

Otimizador compacto para Windows 10 e 11, criado por patoLizo em C# com Avalonia.

## Recursos

- Presets Low, Medium, High, Ultra e Debloat, com oito grupos revisáveis.
- Ajustes visuais, modo gamer e plano de alto desempenho.
- Redução de telemetria, anúncios e sugestões do Windows.
- Bloqueio de Copilot, Recall, Click-to-Do, Widgets e Bing na pesquisa.
- Remoção de aplicativos pré-instalados pelo modo Debloat Extremo.
- Backup local das configurações e comando para desativar otimizações.
- Solicitação de administrador imediatamente na abertura.
- Licença Pro assinada e vinculada ao dispositivo do cliente.
- Modo Windows seco para remover suavização de fontes, sombras e efeitos.
- Pesquisa enxuta sem Bing, destaques e sugestões online.

## Desenvolvimento

Requer o SDK do .NET 10 no Windows.

```powershell
dotnet restore .\PatoLizoOptimizer.Avalonia\PatoLizoOptimizer.csproj
dotnet run --project .\PatoLizoOptimizer.Avalonia\PatoLizoOptimizer.csproj
```

Para gerar um executável único de 64 bits:

```powershell
dotnet publish .\PatoLizoOptimizer.Avalonia\PatoLizoOptimizer.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

Para gerar o executável e o instalador comercial (requer .NET 10 SDK e Inno Setup 6):

```powershell
.\build-release.ps1
```

O instalador é criado em `outputs\installer`, instala em Arquivos de Programas, cria o desinstalador do Windows e exibe o aviso de que a licença fica vinculada ao Device ID.

## Aviso

O software altera configurações administrativas do Windows. Leia os termos apresentados pelo aplicativo antes do uso. Não há garantia de aumento específico de desempenho.

## Licenciamento comercial

O aplicativo valida licenças offline assinadas digitalmente e vinculadas ao Device ID. O gerador está em `tools/Ducktimizer.LicenseGenerator`, mas exige a chave privada do vendedor, que nunca deve ser incluída no repositório ou enviada ao cliente.

O código das versões comerciais é proprietário. Consulte `LICENSE`. Os componentes de terceiros continuam sob suas licenças originais.

## Créditos

Alguns conceitos e mapeamentos de configurações foram adaptados do projeto [Win11Debloat](https://github.com/Raphire/Win11Debloat), disponibilizado sob licença MIT. Consulte `DEPENDENCY-LICENSE`.
