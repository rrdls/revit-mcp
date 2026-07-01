# Revit MCP Design System

Este documento registra o padrão visual usado na landing page em `docs/index.html` e `docs/styles.css`, para orientar novas peças como carrosséis, posts e materiais de divulgação.

## Essencia Visual

O Revit MCP deve parecer uma ferramenta técnica, local e confiável para usuários de Revit. O visual comunica automação real, controle do usuário e integração com ambiente profissional, sem parecer uma landing genérica de IA.

Principios:

- Fundo escuro, interface densa e objetiva.
- Verde como cor principal de ação, conexão e status positivo.
- Azul como apoio para chips, codigo e detalhes tecnicos.
- Painéis inspirados em janelas de software, terminal e ribbon.
- Formas retangulares, bordas finas e poucos efeitos decorativos.
- Texto direto, com beneficio pratico antes do detalhe tecnico.

## Paleta

Use as cores abaixo como base.

| Token | Cor | Uso |
| --- | --- | --- |
| `--bg` | `#101418` | Fundo principal |
| `--panel` | `#171d23` | Paineis e cards |
| `--panel-2` | `#1f2830` | Controles internos e estados secundarios |
| `--text` | `#f2f5f7` | Texto principal |
| `--muted` | `#a6b0b8` | Texto de apoio |
| `--line` | `#33404a` | Bordas e divisorias |
| `--accent` | `#58d68d` | CTA, status ativo, numeracao |
| `--accent-2` | `#61b6ff` | Chips, links tecnicos, destaques auxiliares |
| `--warn` | `#ffcf6a` | Indicador pontual em barras de janela |
| Terminal | `#0c1116` | Blocos de codigo ou output |

Evite gradientes chamativos, roxos dominantes, fundos claros e excesso de neon. O contraste deve continuar alto e legivel.

## Tipografia

Familia principal:

```css
Inter, "Segoe UI", Roboto, Arial, sans-serif
```

Familia mono:

```css
"SF Mono", Consolas, "Liberation Mono", monospace
```

Regras:

- Titulos grandes, firmes e com `letter-spacing: 0`.
- Eyebrows em mono, uppercase, verde.
- Texto de apoio em cinza, com line-height confortavel.
- Codigo, URL, nomes de comandos e respostas do sistema em fonte mono.
- Evite texto longo em slides ou cards pequenos.

## Layout

Padrao da landing:

- Conteudo centralizado com largura maxima de `1160px`.
- Seções separadas por bordas finas.
- Hero em duas colunas: texto forte de um lado, painel de produto do outro.
- Cards em grid, sem cantos arredondados evidentes.
- Espacamento amplo, mas com densidade de ferramenta profissional.

Adaptacao para carrossel:

- Tamanho recomendado: `1080 x 1350px`.
- Margens internas generosas: 72 a 84px.
- Cabecalho pequeno com marca e marcador verde.
- Um conceito principal por slide.
- Um painel visual por slide sempre que possível.
- Rodapé discreto com `Revit MCP` e número do slide.

## Componentes

### Marca

Use `Revit MCP` com um pequeno quadrado verde antes do nome. O quadrado funciona como marca visual simples e também sugere status ativo/conexão.

### Eyebrow

Texto curto em uppercase e fonte mono.

Exemplos:

- `AUTODESK REVIT + IA`
- `MCP EM LINGUAGEM SIMPLES`
- `EXEMPLO PRATICO`

### Painel de Produto

Componente principal para mostrar o fluxo. Deve ter:

- Fundo `#171d23`.
- Borda `#33404a`.
- Barra superior com titulo em cinza.
- Indicador amarelo ou verde na barra.
- Área interna com status, botões, comandos ou output.

### Terminal / Output

Use para representar comandos, respostas e contexto do projeto. Não precisa mostrar código complexo em materiais para não programadores.

Bons exemplos:

```text
Pedido:
"Liste todas as portas do projeto"

Resultado:
42 portas encontradas
```

Evite exemplos que comecem com C#, `Transaction`, `ExternalEvent`, namespaces, API, nomes de função ou JSON, exceto em materiais para desenvolvedores.

Para público não programador, represente contexto técnico como status visual:

```text
Revit aberto
Projeto ativo identificado
Conexão MCP pronta
```

### Chips

Use para resumir capacidades.

Exemplos:

- `Projeto ativo`
- `Ribbon integrado`
- `Consulta de elementos`
- `Automação local`

## Linguagem

Para público não programador, explique o MCP como uma ponte.

Mensagem base:

> MCP é uma ponte padronizada que permite uma IA conversar com ferramentas externas, com permissão do usuário.

Mensagem do produto:

> Revit MCP usa essa ponte para conectar uma IA ao Revit aberto, permitindo consultar informações e executar automações no projeto.

Tom:

- Direto.
- Pratico.
- Sem hype excessivo.
- Técnico o bastante para transmitir confiança.
- Sempre mostrando controle do usuário.

Evite:

- "Execute snippets C# no processo do Revit" em posts para leigos.
- "Servidor WebSocket local", "Roslyn runtime" e detalhes internos.
- JSON, nomes de funções e blocos que pareçam código.
- URLs longas em artes para Instagram.
- Promessas absolutas como "automatize qualquer coisa".

Prefira:

- "A IA entende seu pedido e aciona o Revit MCP."
- "Você revisa o que será feito."
- "Ideal para reduzir tarefas repetitivas no Revit."
- "Acesse o link na bio."

## Direção Para Carrossel

Sequencia recomendada:

1. Gancho com beneficio.
2. Dor do trabalho manual.
3. Explicacao simples de MCP.
4. O que o Revit MCP faz.
5. Exemplos de pedidos.
6. Beneficios praticos.
7. Controle e seguranca.
8. Chamada final.

Cada slide deve funcionar sozinho, mas a sequência deve levar o leitor de "por que isso importa" até "como eu testo".
