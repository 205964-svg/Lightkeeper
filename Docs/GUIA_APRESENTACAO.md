# Lightkeeper — Guia de estudo e apresentação

> Para estudar antes da apresentação (24/11) e responder às perguntas do professor.
> As duas pessoas da dupla precisam saber **tudo** daqui: o professor pode perguntar a qualquer um.

---

## 1. O jogo em 30 segundos (frase de abertura)

> "Lightkeeper é um Tower Defense 2D feito em Unity 6. Criaturas da sombra saem da clareira e seguem
> uma trilha com curvas até o farol. O jogador constrói torres de luz em pedestais para eliminá-las.
> A mecânica própria do jogo é **luz e sombra**: fora do alcance das luzes, as criaturas andam mais rápido.
> Há três dificuldades, três tipos de inimigo, dois tipos de torre e um ranking."

**Tecnologias:** Unity 6 (6000.3.28), C#, URP 2D (luzes 2D), Input System (mouse e toque),
TextMeshPro, ScriptableObjects, Prefabs, PlayerPrefs.

---

## 2. Como o código está organizado (`Assets/Scripts`)

| Pasta | Script | O que faz (em uma frase) |
|---|---|---|
| Core | `GameManager` | Cérebro da partida: vida do farol, luz (dinheiro), abates, tempo, pontuação, vitória/derrota, pausa. |
| Core | `DifficultySettings` | **ScriptableObject** com os dados de cada dificuldade (ondas, tempo, multiplicadores). |
| Core | `GameSession` | Dados **estáticos** que sobrevivem à troca de cena (dificuldade escolhida, nome do jogador). |
| Core | `AudioManager` | Toca sons de qualquer script com `AudioManager.Play(clip)`; ignora clip vazio. |
| Core | `PhysicsUtil` | Garante `Rigidbody2D` **cinemático** (necessário para os triggers). |
| Core | `FadeEffect` | Efeito visual: sprite cresce e some (morte de inimigo). |
| Path | `EnemyPath` | Caminho fixo = lista ordenada de **waypoints** (filhos do objeto Path). |
| Enemies | `Enemy` | Anda pelo caminho, tem vida, recebe dano, fica lento, morre, causa dano ao farol. |
| Enemies | `HealthBar` | Barra de vida acima do inimigo (escala do "Fill"). |
| Enemies | `EnemyVisual` | Animação só visual: flutuar/pisar, balançar, aura pulsando. |
| Waves | `WaveSpawner` | Cria os inimigos onda a onda usando **Coroutine**; avisa o fim das ondas. |
| Towers | `Tower` | Detecta inimigos no raio, escolhe alvo, mira, dispara, espera a recarga. |
| Towers | `Projectile` | Projétil por **translação**, colisão por **trigger**, dano em área e lentidão. |
| Towers | `LightSource` | Área iluminada (luz e sombra). `LightSource.IsLit(posição)`. |
| Towers | `TowerGlow` | Chama/cristal tremulando + luz 2D (só visual). |
| Base | `Lighthouse` | O farol: detecta o inimigo que chega (trigger) e pisca/treme. |
| Building | `BuildSpot` | Pedestal onde se constrói; guarda a torre construída; vende. |
| Building | `BuildManager` | Lê mouse/toque/teclado (Input System) e constrói/vende torres. |
| UI | `HUD`, `MainMenu`, `ButtonHover` | Interface da partida, menu inicial, efeito de hover. |
| Ranking | `RankingManager` | Top 10 salvo em **PlayerPrefs** como **JSON**. |
| Editor | `MenuStyler`, `EnemyStyler`, `TowerStyler`, `DifficultyBuilder` | Ferramentas do editor (menu **Lightkeeper**) que geraram o visual e as dificuldades. Não rodam no jogo. |

---

## 3. Como tudo funciona — explicações para decorar

### 3.1 Ciclo da partida (`GameManager`)
- **Singleton:** `GameManager.Instance` — um só por cena; os outros scripts o chamam direto.
- **Estados:** `Playing`, `Paused`, `Victory`, `Defeat` (enum `GameState`).
- **Eventos:** `StatsChanged` e `StateChanged` (C# `event Action`). A HUD **se inscreve** e só se
  atualiza quando algo muda → a lógica não conhece a interface (baixo acoplamento).
- **Tempo:** `TimeRemaining -= Time.deltaTime` no `Update`. Chegou a 0 → derrota.
- **Pausa e fim:** `Time.timeScale = 0` congela inimigos, torres e projéteis (tudo usa `deltaTime`).

### 3.2 Vitória e derrota
| Situação | Resultado | Onde |
|---|---|---|
| Todas as ondas terminaram **e** abates ≥ mínimo do nível | **Vitória** | `OnAllWavesFinished` |
| Todas as ondas terminaram mas abates < mínimo | Derrota ("missão incompleta") | `OnAllWavesFinished` |
| Vida do farol chegou a 0 | Derrota | `RegisterEnemyAtBase` |
| Inimigos no farol ≥ `maxEnemiesAtBase` (opcional; hoje desligado = 0 nos 3 níveis) | Derrota | `RegisterEnemyAtBase` |
| Tempo acabou | Derrota | `Update` |

**Pontuação:** soma do `scoreValue` de cada abate; na vitória, bônus = vida do farol × 10 + segundos restantes.
O resultado vai para o ranking (`RankingManager.AddEntry`).

### 3.3 Movimento dos inimigos (`Enemy`)
- O caminho é uma lista de **waypoints**. O inimigo nasce no waypoint 0 e vai para o próximo com
  `Vector3.MoveTowards(atual, alvo, velocidade * Time.deltaTime)` → **translação**.
- Chegou perto do alvo (distância² < 0,0001) → passa para o próximo waypoint.
- **Velocidade final** = velocidade × multiplicador da dificuldade × lentidão × (luz ? 1 : `darkSpeedMultiplier`).
- `DistanceTravelled` guarda quanto andou → usado pela mira "Primeiro".
- Lista estática `Enemy.Active` (entra no `OnEnable`, sai no `OnDisable`) → o spawner sabe quando a onda acabou.

### 3.4 Luz e sombra (mecânica própria)
- Cada torre e o farol têm um `LightSource` com um **raio**.
- `LightSource.IsLit(p)` percorre as luzes e testa `distância² ≤ raio²` (evita raiz quadrada).
- Fora de toda luz → o inimigo anda mais rápido (Sombra ×1,3; Colosso ×1,15; Hidra ×1,2).
- **Estratégia:** construir torres também "ilumina" a trilha e desacelera as criaturas.
- A luz que você **vê** é uma `Light2D` do URP; a luz que **conta para o jogo** é o `LightSource`.

### 3.5 Torres: detectar → selecionar → disparar → aguardar (`Tower`)
1. **Detectar:** `Physics2D.OverlapCircle(posição, alcance, filtro, lista)` — todos os colliders no raio.
2. **Selecionar** (`TargetingMode`):
   - `First` — mais adiantado no caminho (maior `DistanceTravelled`) → Lanterna;
   - `Strongest` — mais vida → Prisma; (`Closest` e `Weakest` também existem).
3. **Mirar:** `Mathf.Atan2(dy, dx)` → ângulo → gira a "Cabeca".
4. **Disparar:** `Instantiate(projétil)` + `projectile.Launch(alvo, dano)`.
5. **Aguardar:** `cooldown = 1 / fireRate`. Reavalia o alvo a cada 0,15 s.

| Torre | Custo | Alcance | Disparos/s | Dano | Mira | Projétil |
|---|---|---|---|---|---|---|
| **Lanterna** | 50 | 3 | 2 | 1 | Primeiro | Faísca rápida (12), alvo único |
| **Prisma** | 90 | 4,5 | 0,5 | 3 | Mais forte | Lento (6), **área 1,2** + **lentidão 50% por 2 s** |

Os dois tipos diferem em **tudo** que o enunciado cita: alcance, cadência, dano, projétil e seleção de alvo.

### 3.6 Projéteis e colisão (`Projectile`)
- **Translação:** `homing` = persegue o alvo (`MoveTowards`); se o alvo morre, vai até a última posição.
- **Colisão:** `OnTriggerEnter2D` com um `Enemy` → `Hit`.
- **Área:** `OverlapCircle` no ponto do impacto; `HashSet` evita dar dano duas vezes no mesmo inimigo.
- **Lentidão antes do dano** (o dano pode destruir o inimigo).
- **Destruído quando:** acerta, passa de `maxLifetime` (4 s) ou sai da tela (`WorldToViewportPoint`).

**Por que Rigidbody2D cinemático?** Em 2D, `OnTriggerEnter2D` só dispara se pelo menos um dos objetos
tiver Rigidbody2D. Cinemático = nós movemos pelo Transform, a física só **detecta** (sem gravidade/empurrão).

### 3.7 As 3 colisões obrigatórias do enunciado
| Colisão | Como |
|---|---|
| Projétil × inimigo | `Projectile.OnTriggerEnter2D` → `TakeDamage` |
| Inimigo chega à base | `Lighthouse.OnTriggerEnter2D` → `Enemy.ReachBase` → `GameManager.RegisterEnemyAtBase` |
| Destruição do inimigo | `Enemy.TakeDamage` → vida ≤ 0 → `Die()` → `RegisterKill` + efeito + `Destroy` |

### 3.8 Ondas (`WaveSpawner`)
- **Coroutine** (`IEnumerator` + `yield return`): "espere 1,2 s, crie inimigo, espere..." fica legível.
- `WaitForSeconds` respeita o `timeScale` → pausar o jogo pausa as ondas.
- Por onda: grupos (`WaveGroup`: prefab, quantidade, intervalo, atraso). Espera a onda ser limpa
  (`Enemy.Active.Count == 0`), contagem regressiva, próxima onda. Fim → `OnAllWavesFinished`.

### 3.9 Construção e economia (`BuildManager`, `BuildSpot`)
- **Input System** (`Pointer.current`) → funciona com mouse **e toque** (mobile).
- Converte a tela em mundo (`ScreenToWorldPoint`) e acha o pedestal com `Physics2D.OverlapPointAll`.
- Ignora cliques sobre a UI (`EventSystem.IsPointerOverGameObject`).
- Construir: `GameManager.TrySpend(custo)`. Vender: clique direito → devolve **50%**.
- **Luz por abate** = recompensa do inimigo × `rewardMultiplier` da dificuldade.
- Teclas: **1/2** escolhem a torre, **P/Esc** pausa.

### 3.10 Dificuldades (ScriptableObject `DifficultySettings`)
**Por que ScriptableObject?** Os dados ficam em arquivos (`Assets/Data`), editáveis no Inspector sem mexer
no código. Cada dificuldade tem **suas próprias ondas**.

| | Fácil | Médio | Difícil |
|---|---|---|---|
| Ondas | 3 | 5 | 6 |
| Hidra | — | ondas 4 e 5 | a partir da 2 |
| Vida / velocidade dos inimigos | ×1 / ×1 | ×1,2 / ×1,1 | ×1,3 / ×1,15 |
| Luz por abate | ×1 | ×0,85 | ×0,8 |
| Luz inicial | 120 | 130 | 150 |
| Vida do farol | 20 | 16 | 14 |
| Abates para vencer | 5 | 20 | 25 |
| Tempo máximo | 5:00 | 5:30 | 6:00 |

**Por que a luz por abate diminui nas difíceis?** Elas têm muito mais inimigos; sem o multiplicador a luz
sobraria e as torres ficariam "de graça".

### 3.11 Inimigos
| | Sombra | Colosso | Hidra |
|---|---|---|---|
| Visual | fantasma roxo, olhos amarelos, flutua | golem de pedra, olhos vermelhos, "pisa" | fera vermelha de **duas cabeças** |
| Vida | 4 | 14 | 24 |
| Velocidade | 2,2 | 1,1 | 1,6 |
| Dano ao farol | 1 | 3 | 4 |
| Luz ao morrer (base) | 10 | 25 | 35 |

Diferenciados por **velocidade, resistência e dano à base** (o enunciado pede ao menos duas).

### 3.12 Menu, ranking e cenas
- **Cenas:** `MainMenu` (índice 0) e `GameScene` (índice 1) no Build Profile.
- `MainMenu.StartGame(i)` grava a dificuldade em `GameSession` (estático) e carrega a `GameScene`.
- **Ranking:** lista de `RankingEntry` → `JsonUtility.ToJson` (precisa de uma classe "wrapper", porque
  o JsonUtility não serializa listas soltas) → `PlayerPrefs`. Ordena por pontuação, guarda o top 10.

---

## 4. Requisitos do enunciado × onde está

| Requisito | Onde |
|---|---|
| Menu para iniciar + informações | `MainMenu` (Jogar, Como Jogar, Ranking, Sair) |
| ≥ 1 nível jogável, ≥ 5 inimigos eliminados | 3 níveis; mínimo de 5 / 20 / 30 abates |
| Base, entrada, caminho, posições de torre | Farol, clareira, `EnemyPath` (24 waypoints com curvas), 11 pedestais |
| Espaço para ~30 inimigos enfileirados | Caminho com ~33 unidades; inimigo com ~0,6 |
| ≥ 2 estruturas diferentes | Lanterna × Prisma |
| Detectar, selecionar, disparar, dano, aguardar | `Tower` (seção 3.5) |
| Inimigos: andar, vida, dano, destruir, consequência na base | `Enemy` + `Lighthouse` |
| Projétil por translação, colisão, destruição | `Projectile` |
| Vitória/derrota e tempo máximo | `GameManager` (seção 3.2) |
| **Extra +0,5:** 2º nível de dificuldade | Médio e Difícil |
| **Extra +0,25:** ranking | `RankingManager` |
| Efeitos visuais | Luzes 2D, aura, piscar ao levar dano, tom azul na lentidão, efeito de morte, tremor do farol, hover |
| **Recursos sonoros** | ⚠️ **Hoje só a morte dos inimigos tem som** — ver seção 7 |

---

## 5. Roteiro da apresentação (10 min + 5 de perguntas)

| Tempo | Quem | O quê |
|---|---|---|
| 0:00–1:00 | Pessoa A | Abertura (seção 1) + mostrar o **menu** (Como Jogar, Ranking). |
| 1:00–4:00 | Pessoa B joga, A narra | **Fácil**: construir Lanterna perto da caveira, mostrar alcance (passar o mouse), vender (clique direito), pausa. Explicar luz e sombra. |
| 4:00–5:30 | A | Código: `Tower` (detectar → selecionar → disparar → aguardar) e `Projectile` (translação + trigger). |
| 5:30–7:00 | B | Código: `Enemy` (waypoints + `MoveTowards`), `Lighthouse` (trigger), `WaveSpawner` (coroutine). |
| 7:00–8:30 | B joga | **Médio** até a onda 4 (aparece a **Hidra**) ou Difícil para mostrar a Hidra cedo. Mostrar Prisma (área + lentidão). |
| 8:30–9:30 | A | `GameManager`: estados, eventos, vitória/derrota, tempo. Dificuldades como ScriptableObject. |
| 9:30–10:00 | Os dois | Fim de jogo (tela de vitória/derrota) + ranking atualizado. |

**Dica:** abram o jogo **antes** da aula pelo executável (`Executavel/Lightkeeper-Windows.zip`) e
deixem o projeto aberto na Unity na `GameScene`, com os scripts já abertos no VS Code.

---

## 6. Perguntas prováveis do professor (com respostas)

1. **Como a torre detecta o inimigo?** `Physics2D.OverlapCircle` no raio de alcance; filtra os colliders que têm `Enemy`.
2. **Como escolhe o alvo?** Pelo `TargetingMode`: Lanterna pega o mais adiantado (`DistanceTravelled`), Prisma o com mais vida.
3. **Como o projétil se move?** Translação: a cada frame `MoveTowards` até a posição do alvo, `velocidade × deltaTime`.
4. **Como detecta a colisão?** Collider marcado como **trigger** + `Rigidbody2D` cinemático → `OnTriggerEnter2D`.
5. **Por que cinemático?** Para gerar eventos de trigger sem a física empurrar ou aplicar gravidade; nós movemos pelo Transform.
6. **O que acontece quando o inimigo chega ao farol?** O trigger do `Lighthouse` chama `Enemy.ReachBase` → `GameManager.RegisterEnemyAtBase` → desconta vida, pode causar derrota; o inimigo é destruído.
7. **Por que `Time.deltaTime`?** Para o movimento não depender do FPS (a mesma velocidade em PCs rápidos e lentos).
8. **Como pausa o jogo?** `Time.timeScale = 0`; tudo que usa `deltaTime` e `WaitForSeconds` para.
9. **O que é uma Coroutine?** Função `IEnumerator` que pausa com `yield return` e continua em frames seguintes — usada nas ondas.
10. **O que é ScriptableObject?** Asset de dados do projeto; cada dificuldade é um arquivo editável no Inspector.
11. **Como a dificuldade passa do menu para o jogo?** Campo **estático** em `GameSession` — estáticos não são destruídos na troca de cena.
12. **O que é Singleton?** Uma instância global (`GameManager.Instance`) acessível por todos os scripts.
13. **Como funciona o ranking?** Lista → JSON (`JsonUtility`) → `PlayerPrefs`, ordenada, top 10.
14. **Como a HUD sabe que a vida mudou?** Ela se inscreve no evento `StatsChanged` do `GameManager`.
15. **O que é a mecânica de luz e sombra?** `LightSource.IsLit` — fora da luz, o inimigo multiplica a velocidade.
16. **Como funciona o dano em área do Prisma?** `OverlapCircle` no impacto, raio 1,2; `HashSet` para não repetir; lentidão de 50% por 2 s.
17. **Transformações geométricas usadas?** Translação (movimento), rotação (mira com `Atan2`, `Quaternion.Euler`), escala (barra de vida, alcance, animações, efeitos).
18. **Funciona em mobile?** O `BuildManager` usa `Pointer.current` do Input System, que cobre mouse e toque.
19. **Como o inimigo sabe para onde ir?** `EnemyPath` com waypoints ordenados; `waypointIndex` avança ao chegar em cada um.
20. **Por que a luz por abate muda com a dificuldade?** `rewardMultiplier`: mais inimigos nas difíceis → menos luz por abate, para manter o equilíbrio.

---

## 7. Pendências antes de apresentar (prioridade)

1. ⚠️ **Sons:** o critério "efeitos visuais e **recursos sonoros**" vale 1,0. Hoje só a morte dos inimigos
   tem som; tiro, impacto, música, farol atingido, vitória/derrota e construir/vender estão vazios.
2. **Testar o Difícil** uma vez (o equilíbrio é estimado).
3. **README:** colocar o link do executável e do vídeo (obrigatório pelo enunciado).
4. Nome do executável sai como `Lighkeeper.exe` (falta o "t" em *Player Settings > Product Name*).
5. Cenas que sobraram e não são usadas: `GameScene 1`, `GameSceneBKP`, `SampleScene`; o script `BuildZone` também não é usado — se perguntarem, dizer que são versões de teste.
6. Os **dois** precisam ter commits no GitHub (nota individual de 2,0).
