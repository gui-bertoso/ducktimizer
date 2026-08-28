# PatoLizo Optimizer

Otimizador compacto para Windows 10 e 11, criado em C# com Avalonia.

## Recursos

- Presets Low, Medium, High, Ultra e Debloat.
- Ajustes visuais, modo gamer e plano de alto desempenho.
- Redução de telemetria, anúncios e sugestões do Windows.
- Bloqueio de Copilot, Recall, Click-to-Do, Widgets e Bing na pesquisa.
- Remoção de aplicativos pré-instalados pelo modo Debloat Extremo.
- Backup local das configurações e comando para desativar otimizações.
- Solicitação de administrador imediatamente na abertura.

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

## Aviso

O software altera configurações administrativas do Windows. Leia os termos apresentados pelo aplicativo antes do uso. Não há garantia de aumento específico de desempenho.

## Créditos

Alguns conceitos e mapeamentos de configurações foram adaptados do projeto [Win11Debloat](https://github.com/Raphire/Win11Debloat), disponibilizado sob licença MIT. Consulte `DEPENDENCY-LICENSE`.
