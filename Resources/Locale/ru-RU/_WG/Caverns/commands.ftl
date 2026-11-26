cmd-wgcavern-desc = Осмотр пещер, переход к их вратам, ручное вскрытие входов и замер местности пещер.
cmd-wgcavern-help = Использование: { $command } <list | tp <планета> [pad|mouth] | mouths <планета> | open | stats <планета>>
cmd-wgcavern-disabled = Пещеры выключены. Установите wg.caverns в true.
cmd-wgcavern-invalid-args = Ожидается одно из: list, tp <планета> [pad|mouth], mouths <планета>, open, stats <планета>.
cmd-wgcavern-unknown-planet = Построенной сети планеты «{ $planet }» нет.
cmd-wgcavern-no-cavern = У { $planet } нет пещеры или у её пещеры нет врат.
cmd-wgcavern-empty = Выводить нечего.
cmd-wgcavern-row = { $planet } | пещера: { $cavern } | входы: { $mouths } | внизу: { $players }
cmd-wgcavern-row-none = нет
cmd-wgcavern-mouth-row = { $kind } | якорь: { $origin } | тайлов: { $tiles } | тайл подъёма: { $climb }
cmd-wgcavern-mouth-kind = { $kind ->
    [gate] Врата
    [cell] Ячейка
   *[admin] Админ
}
cmd-wgcavern-tp-done = Перемещено к вратам { $target } планеты { $planet }.
cmd-wgcavern-no-map = Сначала вселитесь в сущность.
cmd-wgcavern-not-ground = Сначала встаньте на поверхность планеты над пещерой.
cmd-wgcavern-open-done = Вскрыт вход вокруг { $origin }.
cmd-wgcavern-open-refused = Здесь нельзя вскрыть вход: { $reason ->
    [grid] над ним корабль или обломки.
    [built] в проёме что-то построено.
    [mob] в проёме кто-то стоит.
    [cavern] у этой планеты нет пещеры.
   *[mouth] здесь уже есть вход.
}
cmd-wgcavern-hint-sub = <list|tp|mouths|open|stats>
cmd-wgcavern-stats-started = Замер { $size }x{ $size } тайлов пещеры вокруг { $centre }...
cmd-wgcavern-stats = { $planet }: { $size }x{ $size } тайлов вокруг { $centre } открыты на { $open }%; крупнейшая область, по которой можно пройти, не пересекая лаву, плазму или кислоту, занимает { $largest }% проходимой земли, руда — { $veins }% породы. В квадрате { $lights } источников света, и { $glow }% пола туннелей в его центре { $inner }x{ $inner } находится в { $walk } тайлах ходьбы от одного из них.
cmd-wgcavern-hint-planet = <название планеты>
cmd-wgcavern-hint-target = <pad|mouth>
