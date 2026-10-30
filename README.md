# Ducktimizer

Otimizador local para Windows 10 e 11, feito por patoLizo com C# e Avalonia.
Gratuito e de código aberto sob a licença MIT. Não exige conta, pagamento,
ativação, identificador de dispositivo ou conexão com um serviço do autor.

## Recursos

- presets e oito grupos revisáveis de ajustes visuais, jogos, energia e privacidade;
- configurações de pesquisa, processos em segundo plano e remoção de aplicativos;
- backup granular antes das alterações e tentativa de criar ponto de restauração;
- restauração das configurações salvas e logs locais.

**Revise cada grupo antes de aplicar.** Remover aplicativos pode ser irreversível:
o backup de Registro e energia não garante reinstalação dos pacotes removidos.
Não há garantia de aumento de FPS. Não execute em computadores sem autorização.

## Desenvolvimento

Requer Windows e SDK do .NET 10:

```powershell
dotnet restore .\PatoLizoOptimizer.Avalonia\PatoLizoOptimizer.csproj
dotnet run --project .\PatoLizoOptimizer.Avalonia\PatoLizoOptimizer.csproj
```

O aplicativo solicita privilégios administrativos para alterar configurações.
O autoteste verifica o catálogo de ajustes e a serialização do backup, sem aplicar mudanças:

```powershell
dotnet run --project .\PatoLizoOptimizer.Avalonia\PatoLizoOptimizer.csproj -- --self-test
```

## Distribuição

```powershell
.\build-release.ps1 -SkipInstaller  # executável independente em outputs/publish
.\build-release.ps1                 # instalador; requer Inno Setup 6
```

Instale atualizações manualmente a partir do código ou das releases do repositório.
Não há atualizador automático, backend comercial ou envio de telemetria.
Os antigos serviços de venda e ativação não fazem parte desta versão.

## Dados locais

Logs e backups permanecem na pasta Ducktimizer dos dados locais do usuário.
A remoção do aplicativo preserva os backups. A versão aberta não lê nem exige
arquivos de ativação de versões anteriores.

## Licença e créditos

O código do projeto usa a [licença MIT](LICENSE).
Os créditos e licenças das fontes de pesquisa estão em
[PatoLizoOptimizer.Avalonia/DEPENDENCY-LICENSE](PatoLizoOptimizer.Avalonia/DEPENDENCY-LICENSE).

## License

The author's code uses the [MIT License](LICENSE). Third-party materials retain their original licenses.
