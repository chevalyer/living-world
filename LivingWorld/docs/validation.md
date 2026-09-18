# проверки 0.6.0-alpha

проверка выполняется автоматически в GitHub Actions на `ubuntu-latest` с .NET 8 и Godot 4.7.1 stable mono. workflow находится в `.github/workflows/validate.yml`.

последний полный run для ветки обновления завершился успешно: **59 regression tests passed, 0 failed**. structural checks, Release/Debug сборки, два headless-прогона симуляции и запуск основной Godot-сцены также прошли.

## что проверяется автоматически

1. `python3 scripts/check_sources.py`;
2. restore всего solution;
3. Release build всего solution;
4. консольный набор regression tests;
5. суточный headless smoke на карте 96 × 96 с 8 жителями;
6. недельный headless run дефолтного мира: seed 1847, 128 × 128, 14 жителей, 10 080 тиков;
7. загрузка Godot 4.7.1 .NET;
8. Debug build Godot-проекта;
9. запуск основной сцены в Godot headless на 300 кадров.

## текущий результат

- structural checks: **479 / 479**;
- C# файлов: **161**;
- actions: **32**;
- component save ids: **25**;
- regression cases: **59 / 59**;
- суточный smoke: 1 440 тиков, 8 жителей живы;
- недельный default-world run: 10 080 тиков, **14 жителей живы**, 5 комнат;
- запуск `Presentation/Main.tscn` через Godot 4.7.1 .NET: успешно;
- в логе финального run нет `SCRIPT ERROR`, C# compile errors или необработанных исключений.

недельный прогон занял около 14,6 секунды на текущем GitHub Actions runner. это проверка стабильности, а не обещание производительности на другом компьютере.

## новые проверки 0.6.0-alpha

добавлены отдельные regression cases для:

- объединения близких домов в одно поселение;
- разделения удалённых поселений;
- стабильности сгенерированного имени;
- границ поселения;
- уникальной принадлежности жителя без дома только одному ближайшему поселению;
- read-only данных поселения в render snapshot;
- независимости трёх save-файлов.

structural checks также требуют совпадения версии в `VERSION`, `project.godot` и README.

## что автоматическая проверка не подтверждает

Godot headless подтверждает, что сцена, новый HUD, сигналы и C#-скрипты создаются и работают без runtime-ошибок в проверяемом сценарии. он **не заменяет визуальную проверку окна**: внешний вид, читаемость на разных разрешениях и ощущения от цветов нужно дополнительно смотреть обычным запуском игры.

долгосрочный баланс нескольких поколений и производительность очень крупных поселений этим обновлением не проверяются.

## локальный запуск проверок

из папки `LivingWorld`:

```bash
python3 scripts/check_sources.py
dotnet build LivingWorld.sln -c Release --nologo
dotnet run --project Tests/LivingWorld.Tests.csproj -c Release
dotnet run --project Headless/LivingWorld.Headless.csproj -c Release -- --seed 1847 --size 128 --npcs 14 --ticks 10080 --json artifacts/week.json
```

для обычной визуальной проверки открой `project.godot` в Godot 4.7.1 .NET и запусти проект.
