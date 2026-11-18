# Montagem das cenas no Unity

## Jeito rápido: montador automático

O script de editor `Assets/Scripts/Editor/LightkeeperBuilder.cs` monta tudo sozinho:
sprites geométricos, sons sintetizados (`.wav`), prefabs, as dificuldades Fácil e Difícil e as cenas
`MainMenu` e `GameScene` (com o trajeto seguindo a estrada desenhada em `Fundo.jpeg`).

- **Lightkeeper > Montar jogo completo**: recria `Assets/Generated` e **sobrescreve** as duas cenas.
- **Lightkeeper > Gerar executável Windows**: gera `Builds/Windows/Lightkeeper.exe`.

Os valores (vida, velocidade, custo, ondas, posições do caminho e dos pedestais) estão no topo e nos
métodos `CreatePrefabs` / `CreateDifficulties` do builder. Dá para ajustar lá e montar de novo, ou
editar direto os prefabs/assets em `Assets/Generated` (mas rodar o montador de novo desfaz isso).

### Teste automático
Ferramenta de desenvolvimento (`Assets/Scripts/Debug/AutoTest.cs`), só ativa com argumento:
```
Lightkeeper.exe -autotest C:\pasta\saida -autotest-difficulty 0
```
Joga sozinho (compra torres, acelera o tempo), salva prints e um `autotest.log` com o resultado.

---

## Jeito manual (para entender as peças)

Os scripts ficam em `Assets/Scripts`. As seções abaixo mostram como ligá-los na cena à mão.

> Projeto configurado com o **Input System novo** (Player Settings > Active Input Handling = Input System Package).
> O `BuildManager` já usa `Pointer`/`Mouse`/`Keyboard` desse pacote, então funciona com mouse e com toque (mobile).

## 1. Prefabs

### Projéteis (`Assets/Prefabs/Projectiles`)
| Prefab | Componentes | Configuração sugerida |
|---|---|---|
| **Projetil_Faisca** | SpriteRenderer (círculo pequeno amarelo), CircleCollider2D, `Projectile` | speed 12, homing ✔, splashRadius 0, slowMultiplier 1 |
| **Projetil_Prisma** | SpriteRenderer (círculo azul-claro), CircleCollider2D, `Projectile` | speed 6, homing ✔, splashRadius 1.2, slowMultiplier 0.5, slowDuration 2 |

O Rigidbody2D cinemático é adicionado automaticamente pelo script.

### Torres (`Assets/Prefabs/Towers`)
| Prefab | Script `Tower` | Diferença |
|---|---|---|
| **Torre_Lanterna** | cost 50, range 3, fireRate 2, damage 1, targeting **First**, projectile = Projetil_Faisca | rápida, dano baixo, alvo único |
| **Torre_Prisma** | cost 90, range 4.5, fireRate 0.5, damage 3, targeting **Strongest**, projectile = Projetil_Prisma | lenta, dano em área + lentidão |

Estrutura sugerida de cada torre:
```
Torre_Lanterna        (Tower, LightSource radius 3)
 ├─ Base              (SpriteRenderer)
 ├─ Cabeca            (SpriteRenderer)  -> arraste em "Rotating Part"
 │   └─ FirePoint     (vazio, na ponta)  -> arraste em "Fire Point"
 └─ Alcance           (SpriteRenderer círculo 1x1, cor translúcida) -> "Range Indicator"
```
Opcional: adicione uma **Light 2D (Point)** com o mesmo raio do LightSource para o efeito visual de luz.

### Inimigos (`Assets/Prefabs/Enemies`)
| Prefab | Configuração sugerida |
|---|---|
| **Sombra** | maxHealth 4, speed 2.2, baseDamage 1, reward 10, darkSpeedMultiplier 1.3 |
| **Colosso** | maxHealth 14, speed 1.1, baseDamage 3, reward 25, darkSpeedMultiplier 1.15 (sprite maior) |

Cada inimigo: SpriteRenderer + CircleCollider2D + `Enemy`. Para barra de vida, crie um filho
`HealthBar` (script `HealthBar`) com um sprite de fundo escuro e um filho `Fill` (sprite verde,
pivô à esquerda) e arraste o Fill no campo *Fill*.

## 2. Dificuldades (`Assets/Data`)
*Botão direito > Create > Lightkeeper > Dificuldade*.

**Facil** — baseMaxHealth 20, timeLimit 300, minKillsToWin 5, startingCurrency 120,
multiplicadores 1 / 1. Ondas:
1. 6× Sombra (intervalo 1.2)
2. 8× Sombra (0.9) + 2× Colosso (2.5)
3. 10× Sombra (0.7) + 4× Colosso (2)

**Dificil** — baseMaxHealth 12, maxEnemiesAtBase 8, timeLimit 240, minKillsToWin 15,
startingCurrency 90, vida ×1.4, velocidade ×1.2. Ondas: 5 ondas crescentes, misturando os dois tipos.

## 3. Cena `GameScene`
```
GameScene
 ├─ Main Camera              (ortográfica, size ~6–7)
 ├─ Global Light 2D          (intensidade baixa, ~0.3, para o cenário ficar escuro)
 ├─ GameManager              (GameManager, AudioManager) -> Default Difficulty = Facil
 ├─ Path                     (EnemyPath)
 │   ├─ WP_00 (entrada, fora da tela) ... WP_N (sobre o farol)   <- curvas!
 ├─ Farol                    (SpriteRenderer, BoxCollider2D, Lighthouse, LightSource radius 2.5)
 ├─ BuildSpots
 │   ├─ Spot_01 ... Spot_08  (SpriteRenderer quadrado, BoxCollider2D, BuildSpot)
 ├─ Spawner                  (WaveSpawner) -> Paths = [Path], Enemies Parent = Enemies
 ├─ BuildManager             (BuildManager) -> Tower Prefabs = [Torre_Lanterna, Torre_Prisma]
 ├─ Enemies / Defenses       (vazios, só organização)
 ├─ EventSystem              (com "Input System UI Input Module")
 └─ GameCanvas               (HUD)
     ├─ textos: Vida, Luz, Onda, Tempo, Abates, Mensagem
     ├─ BotaoTorre1, BotaoTorre2, BotaoVender, BotaoPausa, BotaoProximaOnda
     ├─ PausePanel  (Continuar -> HUD.OnPauseButton, Menu -> HUD.OnMenuButton)
     └─ EndPanel    (Título, Detalhes, Reiniciar -> HUD.OnRestartButton, Menu -> HUD.OnMenuButton)
```
Escala: o caminho precisa caber ~30 inimigos enfileirados. Com inimigos de ~0.6 unidade,
um caminho com 20+ unidades de comprimento já atende.

## 4. Cena `MainMenu`
```
MainMenu
 ├─ Main Camera, EventSystem
 └─ Canvas (MainMenu)  -> Difficulties = [Facil, Dificil], Game Scene Name = "GameScene"
     ├─ MainPanel:    Título, InputField nome, Jogar Fácil -> StartGame(0), Jogar Difícil -> StartGame(1),
     │                Como Jogar -> ShowInfo, Ranking -> ShowRanking, Sair -> Quit
     ├─ InfoPanel:    texto com regras/controles, Voltar -> ShowMain
     └─ RankingPanel: RankingText, Limpar -> ClearRanking, Voltar -> ShowMain
```

## 5. Build Settings
*File > Build Profiles > Scene List*: adicione **MainMenu** (índice 0) e **GameScene** (índice 1).

## 6. Checklist dos requisitos do trabalho
| Requisito | Onde está |
|---|---|
| Menu para iniciar | `MainMenu.cs` |
| Base com vida / derrota | `Lighthouse.cs` + `GameManager.RegisterEnemyAtBase` |
| Caminho fixo com curvas | `EnemyPath.cs` (waypoints) |
| ≥ 2 tipos de torre | prefabs Lanterna × Prisma (`Tower.cs` + `TargetingMode`) |
| Detecção por raio, alvo, disparo, recarga | `Tower.Update / FindTarget / Fire` |
| Projétil por translação + colisão + destruição | `Projectile.cs` |
| Inimigos com vida, dano, destruição, dano à base | `Enemy.cs` |
| ≥ 2 tipos de inimigo | Sombra × Colosso (velocidade, vida, dano à base) |
| Ondas, vitória, derrota, tempo máximo | `WaveSpawner.cs` + `GameManager.cs` |
| Mínimo de 5 abates | `DifficultySettings.minKillsToWin` |
| Efeitos visuais/sonoros | flash de dano, lentidão, tremor do farol, prefabs de efeito, `AudioManager` |
| Extra: 2º nível de dificuldade (+0,5) | segundo `DifficultySettings` |
| Extra: ranking (+0,25) | `RankingManager.cs` |
