# Словарь полей RecipeStudio (код ↔ Excel/TSV ↔ БД)

| Семантика | Модель (C#) | Excel/TSV header | SQLite column |
|---|---|---|---|
| Код рецепта | `RecipeCode` | `recipe_code` | `recipe_code` |
| Номер точки | `NPoint` | `n_point` | `n_point` |
| Активность | `Act` | `Act` | `act` |
| Safe | `Safe` | `Safe` | `safe` |
| Радиус | `RCrd` | `r_crd` | `r_crd` |
| Высота | `ZCrd` | `z_crd` | `z_crd` |
| Положение (верх/низ) | `Place` | `place` | `place` |
| Скрыть | `Hidden` | `hidden` | `hidden` |
| Диаметр сопла | `ANozzle` | `a_nozzle` | `a_nozzle` |
| Рек. альфа | `RecommendedAlfa` | `recommended_alfa` | `recommended_alfa` |
| Угол альфа | `Alfa` | `alfa_crd` | `alfa` |
| Угол бета | `Betta` | `betta_crd` | `betta` |
| Скорость стола | `SpeedTable` | `speed_table` | `speed_table` |
| Время прохода | `TimeSec` | `time_sec` | `time_sec` |
| Скорость сопла | `NozzleSpeedMmMin` | `v_mm_min` | `nozzle_speed_mm_min` |
| Рек. расход | `RecommendedIceRate` | `recommended_ice_rate` | `recommended_ice_rate` |
| Расход | `IceRate` | `ice_rate` | `ice_rate` |
| Фракция льда | `IceGrind` | `ice_grind` | `ice_grind` |
| Давление воздуха | `AirPressure` | `air_pressure` | `air_pressure` |
| Температура воздуха | `AirTemp` | `air_temp` | `air_temp` |
| Контейнер | `Container` | `container` | `container` |
| D формы | `DClampForm` | `d_clamp_form` | `d_clamp_form` |
| D контейнера | `DClampCont` | `d_clamp_cont` | `d_clamp_cont` |
| Комментарий | `Description` | `description` | `description` |
| Выходы CALC/SAVE | `Xr0...ClampPuls` | `Xr0...Clamp_puls` | `xr0...clamp_puls` |

## Поддерживаемые алиасы Excel

Основные: `a_nozz`, `betta_c`, `beta_crd`, `speed_tab`, `ice_grin`, `air_pre`, `air_tem`, `contai`, `d_clamp_f`, `d_clamp`, `alfa_rec`, `recom_alfa`, `time`, `t_sec`, `v`.

Актуальный список хранится в `RecipeFieldCatalog.ExcelAliases`.
