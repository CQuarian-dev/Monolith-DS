wg-shuttle-console-enter-atmosphere = Войти в атмосферу: { $planet }
wg-shuttle-console-atmosphere-mode-tooltip = В атмосфере:
    Обычные двигатели дают половину тяги.
    Посадочные двигатели дают полную тягу.
    Для взлёта и зависания подъёмная сила должна достигнуть 100%.
wg-flight-phase-orbit = На орбите: { $planet }
wg-flight-phase-landed = Посадка: { $planet }
wg-flight-phase-lifting = Взлёт
wg-flight-phase-spooling = Разгон двигателей: { $countdown } с
wg-flight-phase-climbing = Набор высоты
wg-flight-phase-descending = Снижение
wg-flight-phase-hovering = Зависание
wg-flight-phase-lift-lost = ПОТЕРЯ ПОДЪЁМНОЙ СИЛЫ: падение
wg-flight-ready = ГОТОВ К ВЗЛЁТУ
wg-flight-not-ready = НЕ ГОТОВ
wg-flight-check-lift = Подъёмная сила
wg-flight-check-lift-value = { $percent }% / 100%
wg-flight-check-power = Питание
wg-flight-check-power-ok = Норма, { $power } кВт
wg-flight-check-power-short = Не хватает { $short } кВт
wg-flight-check-hold = Удерживается: { $reason }
wg-flight-check-cargo = Груз
wg-flight-check-cargo-value = { $tonnes } т
wg-flight-fix-convert = Решение: переоборудуйте { $count } { $count ->
        [one] двигатель
        [few] двигателя
       *[other] двигателей
    } в посадочные.
wg-flight-fix-add = Решение: добавьте ещё { $count } { $count ->
        [one] посадочный двигатель
        [few] посадочных двигателя
       *[other] посадочных двигателей
    }.
wg-flight-fix-convert-add = Решение: переоборудуйте все { $convert } обычных двигателей и добавьте { $add } { $add ->
        [one] посадочный
        [few] посадочных
       *[other] посадочных
    }.
wg-flight-fix-power = Решение: добавьте { $short } кВт питания.
wg-flight-hint-grounded = Удерживайте подъём или нажмите «Взлёт», чтобы взлететь.
wg-flight-hint-air = Подъём и спуск меняют высоту.
wg-flight-hint-orbit = Удерживайте спуск, чтобы войти в атмосферу.
wg-flight-landing-blocked = Здесь не сесть. Под корпусом что-то твёрдое.
wg-flight-busy = Этот корпус уже в движении.
wg-flight-descent-refused = Вход в атмосферу отклонён.
wg-flight-lift-warning = Подъёмная сила всего { $percent }%. Чтобы всё равно спуститься, нажмите «Войти в атмосферу» на консоли.
wg-flight-power-warning = Не хватает питания для зависания. Чтобы всё равно спуститься, нажмите «Войти в атмосферу» на консоли.
wg-flight-confirm-title = Вход в атмосферу
wg-flight-confirm-descend = Спуститься
wg-flight-confirm-abort = Остаться на орбите
wg-flight-confirm-text = { $planet }: подъёмная сила [color={ $colour }]{ $percent }%[/color] из 100%, нужных для зависания.
    Мощность для зависания: { $power } кВт.
wg-flight-confirm-low-lift = Не хватает подъёмной силы: корпус будет проседать и не сможет подняться обратно.
wg-flight-confirm-falling = Подъёмной силы слишком мало: корпус упадёт и разобьётся.
wg-flight-confirm-power-deficit = Не хватает питания: двигатели будут отключаться.
wg-thruster-atmosphere-mode = Атмосферный режим: половина тяги. Посадочное переоборудование вернёт полную тягу.
wg-thruster-landing-converted = Посадочное переоборудование: полная тяга в атмосфере.
wg-landing-kit-wrong-target = Используйте набор на линейном двигателе, а не на гироскопе.
wg-landing-kit-not-anchored = Закрепите двигатель перед переоборудованием.
wg-landing-kit-converted = Посадочное переоборудование установлено: полная тяга в атмосфере.
wg-alert-lift-lost = Потеря подъёмной силы
wg-alert-lift-lost-desc = Корпусу больше не хватает подъёмной силы, чтобы держаться в воздухе.
wg-alert-lift-lost-announcement = Внимание: потеря подъёмной силы. Корпус снижается.
wg-alert-dont-sink = Не снижаться
wg-alert-dont-sink-desc = Корабль сошёл с орбиты и погружается в атмосферу.
wg-alert-dont-sink-announcement = Не снижаться. { $ship } сошёл с орбиты и падает.
wg-alert-sink-rate = Скорость снижения
wg-alert-sink-rate-desc = Снижение быстрее, чем корпус способен остановить.
wg-alert-sink-rate-announcement = Скорость снижения. Скорость снижения.
wg-alert-terrain = Земля
wg-alert-terrain-desc = Земля приближается.
wg-alert-terrain-announcement = Земля. Впереди земля.
wg-alert-too-low-terrain = Слишком низко — земля
wg-alert-too-low-terrain-desc = Ниже последнего безопасного слоя без подъёмной силы.
wg-alert-too-low-terrain-announcement = Слишком низко. Земля.
wg-alert-pull-up = Тяни вверх
wg-alert-pull-up-desc = До удара секунды.
wg-alert-pull-up-announcement = Тяни вверх. Тяни вверх.
wg-shuttle-console-orbit-stable = Орбита: стабильна
wg-shuttle-console-orbit-decaying = Орбита снижается: { $seconds } с
wg-alert-orbit-decay = Снижение орбиты
wg-alert-orbit-decay-desc = Удержание позиции потеряно, корпус сходит с орбиты.
wg-alert-orbit-decay-announcement = Орбита снижается: удержание позиции потеряно. { $ship } сходит с орбиты.
wg-alert-orbit-decay-countdown = Орбита снижается: удержание позиции потеряно, до входа в атмосферу { $seconds } с.
wg-landing-kit-already-converted = Этот двигатель уже переоборудован под посадку.
wg-landing-kit-out-of-reach = Подойдите ближе, чтобы установить посадочное переоборудование.
wg-crash-apc-fault-examine = Регулятор питания повреждён и периодически отключается. Откалибруйте его мультитулом.
wg-crash-apc-fault-repaired = Вы калибруете повреждённый регулятор ЛКП. Выход снова стабилен.
wg-orbit-flight-refused = Здесь, наверху, нет воздуха для полёта.
wg-orbit-flight-stopped = Здесь, наверху, нет воздуха, и вы перестаёте лететь.
