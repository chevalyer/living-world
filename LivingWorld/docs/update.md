# обновление 0.5.0-alpha — отдельные definition-файлы

это обновление меняет только способ хранения игровых определений. runtime-модель симуляции остается совместимой: системы по-прежнему работают через `DefinitionCatalog`.

## новая структура

- каждый предмет хранится в отдельном `Definitions/Data/Items/<id>.json`;
- каждый материал — в `Definitions/Data/Materials/<id>.json`;
- каждое растение — в `Definitions/Data/Plants/<id>.json`;
- каждое здание — в `Definitions/Data/Buildings/<id>.json`;
- правила имен перенесены в `Definitions/Data/Names/names.json`;
- общий `recipes.json` удален;
- рецепты теперь находятся внутри json предмета, который они создают.

загрузчик рекурсивно сканирует папки, поэтому позже можно добавлять `Items/Tools`, `Items/Food`, `Buildings/Production` и другие подпапки без изменения кода.

## рецепты

встроенный рецепт не содержит `output`: результат определяется файлом предмета. при загрузке `DefinitionLoader` создает обычные `RecipeDefinition` и добавляет их в `DefinitionCatalog.Recipes`, поэтому `CraftAction`, planner и остальные системы не требуют отдельной логики.

## совместимость

семантические `ItemDefinition` и `RecipeDefinition` в runtime не изменены. fingerprint по-прежнему строится по тем же runtime-определениям, поэтому перенос файлов сам по себе не должен делать существующие сохранения несовместимыми.

## затронутый код

- `Infrastructure/DefinitionLoader.cs` — рекурсивная загрузка отдельных файлов и извлечение встроенных рецептов;
- `Presentation/GameRoot.cs` — перечисление definition-файлов внутри `res://`;
- `Headless/LivingWorld.Headless.csproj` и `Tests/LivingWorld.Tests.csproj` — рекурсивное копирование data-файлов;
- `Tests/TestSuite.cs` — проверки измененных/additive definitions через новую файловую структуру;
- `scripts/check_sources.py` — структурная проверка отдельных json-файлов;
- `docs/extending.md` — новая инструкция по расширению данных.

версия проекта установлена в `0.5.0-alpha` через `application/config/version`.

`main` этим обновлением не изменяется: работа ведется в отдельной ветке.
