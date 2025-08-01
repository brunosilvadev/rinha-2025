# Rinha Stress Tester

> Este arquivo README, bem como toda esta ferramenta de teste foi construída no estilo vibe coding usando GitHub Copilot Agent Mode baseado no modelo Claude Sonnet 4

Uma ferramenta abrangente de teste de carga .NET 9 para o desafio Rinha de Backend 2025 com recursos avançados incluindo **simulação de delay no meio do teste**.

## 🔥 Novos Recursos

### Sistema de Condições de Estresse Aleatórias
O testador de estresse usa um **sistema de rolagem de dados** para aplicar aleatoriamente condições de estresse durante os testes:

- **Rolagem 1d4** determina qual condição de estresse aplicar
- **25% de chance cada** para: Delay+falha, Apenas falha, Apenas delay, ou Sem estresse
- **Pode ser desabilitado** com a flag `--no-stress` para testes limpos garantidos
- **Aplicado em pontos aleatórios** durante o teste (entre 10% e 90% de conclusão)

### Condições de Estresse Aplicadas
Quando o estresse é acionado, pode incluir:

- **Alta Latência**: Define delay de 1250ms no processador padrão
- **Falhas**: Habilita falhas no processador padrão  
- **Timing Aleatório**: Aplicado em 1-3 pontos aleatórios durante a execução do teste
- **Reset Automático**: Todas as condições são resetadas após a conclusão do teste

Isso simula cenários imprevisíveis do mundo real durante picos de carga!

## Uso

### Uso Básico
```bash
# Teste de estresse básico com condições aleatórias (sistema de rolagem 1d4)
dotnet run -- -r 1000 -t 20

# Teste limpo SEM condições de estresse (garantido)
dotnet run -- -r 1000 -t 20 --no-stress

# Teste de carga mais alta com condições aleatórias
dotnet run -- -r 5000 -t 50 --url http://localhost:9999

# Definir delay do processador manualmente (modo utilitário - sem teste de estresse)
dotnet run -- --set-delay 1250 --processor default
dotnet run -- --set-delay 500 --processor fallback
dotnet run -- --set-delay 0 --processor default  # Resetar delay
```

### Opções de Linha de Comando

| Opção | Curta | Descrição | Padrão |
|-------|-------|-----------|---------|
| `--requests` | `-r` | Número de requisições para enviar (obrigatório para teste de estresse) | - |
| `--threads` | `-t` | Número de threads concorrentes | 10 |
| `--url` | `-u` | URL base para a API | http://localhost:9999 |
| `--set-delay` | - | Definir delay no processador em milissegundos (modo utilitário) | - |
| `--processor` | - | Tipo de processador: default ou fallback | default |
| `--no-stress` | - | Desabilitar condições de estresse aleatórias (forçar teste limpo) | false |
| `--help` | `-h` | Mostrar mensagem de ajuda | - |

**Nota**: Por padrão, condições de estresse são aplicadas aleatoriamente via rolagem 1d4. Use `--no-stress` para garantir nenhuma condição de estresse.

## Scripts de Demonstração

Como as condições de estresse agora são aleatórias, os scripts de demonstração mostrariam diferentes padrões de estresse:

1. **`basic-test.bat`** - Teste de estresse básico com condições aleatórias
2. **`high-load-test.bat`** - Teste de carga alta com condições aleatórias  
3. **`utility-delay-test.bat`** - Utilitários de configuração de delay manual
4. **`clean-test.bat`** - Resetar todos os delays antes de testar

## Como Funciona o Estresse Aleatório

### Sistema de Rolagem de Dados
1. No início do teste, rola **1d4** para determinar condição de estresse:
   - **Rolagem 1**: Ambos alta latência (1250ms) e falhas
   - **Rolagem 2**: Apenas falhas
   - **Rolagem 3**: Apenas alta latência (1250ms)  
   - **Rolagem 4**: Nenhuma condição de estresse

### Pontos de Aplicação Aleatórios
2. Se o estresse estiver habilitado, aplica em **1-3 índices de requisição aleatórios**:
   - Aplicado entre 10% e 90% do total de requisições
   - Múltiplos pontos de estresse possíveis em um teste
   - Cada ponto de aplicação aparece nos logs

3. **Limpeza automática** reseta todas as condições após conclusão do teste

Isso simula cenários do mundo real como:
- Lentidões imprevisíveis do banco de dados durante tráfego de pico
- Picos aleatórios de latência de rede
- Restrições súbitas de recursos
- Degradações de serviços externos
- **Falhas aleatórias do processador e recuperação**
- **Ativação inesperada de circuit breaker do serviço**
- **Cenários reais de engenharia do caos**

## URLs dos Processadores

A ferramenta tem como alvo estes endpoints de processador:
- **Processador Padrão**: `http://localhost:8001`
- **Processador de Fallback**: `http://localhost:8002`
- **API Principal**: `http://localhost:9999` (padrão)

## Compilando e Executando

### Compilar o projeto:
```bash
dotnet build
```

### Executar o testador de estresse:
```bash
dotnet run -- [opções]
```

### Executar do executável publicado:
```bash
dotnet publish -c Release
./bin/Release/net9.0/RinhaStressTester.exe [opções]
```

## Exemplo de Saída

```
info: RinhaStressTester.Program[0]
      Iniciando teste de estresse com 500 requisições usando 20 threads
info: RinhaStressTester.Program[0]
      URL de destino: http://localhost:9999
info: RinhaStressTester.Program[0]
      🔥 Ambas as mudanças de DELAY e FALHA no meio do teste estão HABILITADAS - Teste dinâmico de resiliência ativo!
info: RinhaStressTester.Program[0]
      Geradas 500 requisições de pagamento
info: RinhaStressTester.Program[0]
      Mudança de delay no meio do teste será acionada em aproximadamente 12 segundos
info: RinhaStressTester.Program[0]
      Mudança de falha no meio do teste será acionada em aproximadamente 14 segundos
info: RinhaStressTester.Program[0]
      🔄 ACIONANDO MUDANÇA DE DELAY NO MEIO DO TESTE - Definindo delay para 1250ms no processador padrão
info: RinhaStressTester.Program[0]
      ✅ Delay definido com sucesso para 1250ms no processador Padrão
info: RinhaStressTester.Program[0]
      ⏳ Aguardando 3 segundos com delay aumentado...
info: RinhaStressTester.Program[0]
      🔥 ACIONANDO MUDANÇA DE FALHA NO MEIO DO TESTE - Habilitando falhas no processador padrão
info: RinhaStressTester.Program[0]
      ✅ Falhas HABILITADAS com sucesso no processador Padrão
info: RinhaStressTester.Program[0]
      🔄 RESETANDO DELAY - Definindo delay de volta para 0ms no processador padrão
info: RinhaStressTester.Program[0]
      ✅ Delay definido com sucesso para 0ms no processador Padrão
info: RinhaStressTester.Program[0]
      ✅ Sequência de mudança de delay no meio do teste concluída
info: RinhaStressTester.Program[0]
      ⏳ Aguardando 3 segundos com falhas habilitadas...
info: RinhaStressTester.Program[0]
      🔄 RESETANDO FALHA - Desabilitando falhas no processador padrão
info: RinhaStressTester.Program[0]
      ✅ Falhas DESABILITADAS com sucesso no processador Padrão
info: RinhaStressTester.Program[0]
      ✅ Sequência de mudança de falha no meio do teste concluída

=== RESULTADOS DO TESTE DE ESTRESSE ===
Tempo Total: 15.67 segundos
Total de Requisições: 500
Requisições por Segundo: 31.91
Threads Concorrentes: 20

=== ESTATÍSTICAS DE RESPOSTA ===
Requisições Bem-sucedidas: 487 (97.40%)
Requisições Falhadas: 8 (1.60%)
Requisições com Erro: 5 (1.00%)

=== ESTATÍSTICAS DE TEMPO DE RESPOSTA ===
Tempo Médio de Resposta: 456.78 ms
Tempo Mínimo de Resposta: 45.23 ms
Tempo Máximo de Resposta: 1,678.90 ms
50º Percentil (Mediana): 234.56 ms
95º Percentil: 1,345.67 ms
99º Percentil: 1,567.89 ms

=== DETALHAMENTO POR CÓDIGO DE STATUS ===
201: 487 requisições
429: 8 requisições
500: 5 requisições
```

## Recursos

- **Volume de Requisições Configurável**: Especifique o número de requisições para enviar
- **Threading Concorrente**: Controle o número de threads concorrentes para teste de carga
- **Simulação de Delay no Meio do Teste**: Testa automaticamente a resiliência do sistema durante picos de latência
- **Simulação de Falha no Meio do Teste**: Testa automaticamente a resiliência do sistema durante falhas do processador
- **Configuração do Processador**: Defina delays manualmente nos processadores padrão/fallback
- **Estatísticas Detalhadas**: Obtenha métricas abrangentes de performance incluindo:
  - Percentis de tempo de resposta (50º, 95º, 99º)
  - Taxas de sucesso/falha
  - Detalhamento de códigos de status HTTP
  - Requisições por segundo
- **Dados de Teste Realistas**: Gera requisições de pagamento aleatórias com valores variados
- **Progresso em Tempo Real**: Mostra atualizações de progresso durante a execução do teste

## Observações

- O testador de estresse gera dados de pagamento realistas com valores aleatórios entre R$ 0,01 e R$ 1.000,00
- Cada requisição inclui um ID de correlação único
- Atualizações de progresso são mostradas a cada 100 requisições
- Todas as requisições são feitas concorrentemente dentro do limite de threads especificado
- Mudança de delay no meio do teste requer que o processador padrão esteja rodando em `http://localhost:8001`
- Simulação de falha no meio do teste requer que o processador padrão esteja rodando em `http://localhost:8001`
- Usa instâncias separadas de HttpClient para evitar interferência entre teste de estresse e chamadas de configuração
