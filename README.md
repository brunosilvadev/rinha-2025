# Rinha de Backend 2025 - Submissão em .NET 9

Este projeto é uma submissão para a Rinha de Backend 2025, implementada em .NET 9.

O load balance foi feito com nginx
A API sobe duas instâncias, que compartilham o mesmo redis
Nesse redis são armazenados os totais de transações para consulta
Também no redis há um cache da saúde dos payment processors

## Circuit Breaker
Esse cache da saúde segue o padrão circuit breaker, para que uma requisição que falhar ou demorar demais para responder seja imediatamente redirecionada ao processador fallback e o "breaker" seja marcado como aberto (faulty) e todas as requisições são redirecionadas ao processador fallback. Depois de 5 segundos, é feita uma checagem na saúde do processador default e se ele estiver ok, o breaker fica "semi-aberto". Se nessa condição houverem 3 requisições com sucesso, ele volta a fechar e se torna o processador padrão novamente.

## SemaphoreSlim
O cache de saúde usa a classe System.Threading.SemaphoreSlim para limitar uma única requisição por API, cujo response imediatamente é salvo no cache redis. Assim caso a outra API precise do resultado, já estará salvo. Ponto importante: as requisições enviadas ao redis que não precisam de retorno foram feitas no formato "fire and forget". O código não espera a resposta.

## DecisionService
A lógica principal está na classe DecisionService, que vai seguir a seguinte ordem de decisão: 

1. Se ambos serviços estão ok, recomenda o default processor
2. Se o default processor estiver em falha ou alta latência, checa a saúde do fallback
3. Se o fallback processor estiver okay, recomenda o fallback
4. Se o fallback estiver em falha ou alta latência também, recomenda o default processor (seguindo a lógica "fail fast")

## Misc
No Program.cs eu fiz algumas configurações para otimizar performance, como declarar os serviços em Singleton e reutilizar a mesma instância de httpClient com pool de conexões. Além disso na pasta RinhaStressTester tem um aplicativo de linha de comando feito para simular localmente o cenário de teste deste desafio, mas foi totalmente feito com ajuda de AI e absolutamente mandrake.
---

## TODO

- [x] Implementar a lógica para decidir qual payment processor utilizar (rate limiting, caching)
- [x] Passar testes de performance
- [x] Avaliar necessidade de usar fila DLQ
- [x] Calcular taxa cobrada total final
- [x] Limpar comentários
- [x] Revisar configurações de CPU e memória
- [x] Remover lógica desnecessária para minimizar o pacote
- [x] Remover arquivos desnecessários do repositório
- [x] Melhorar o README do stress tester
- [x] Fazer o PR para mandar

---

# Integração com Payment Processors - Rinha 2025

## Configuração Docker Compose

Este projeto foi configurado para integrar com os Payment Processors da Rinha de Backend 2025.

### Pré-requisitos

1. **Subir os Payment Processors primeiro:**
   ```bash
   # Clone o repositório da Rinha
   git clone https://github.com/zanfranceschi/rinha-de-backend-2025.git
   
   # Navegue até o diretório dos payment processors
   cd rinha-de-backend-2025/payment-processor
   
   # Suba os payment processors
   docker-compose up -d
   ```

2. **Subir sua aplicação:**
   ```bash
   # No diretório do seu projeto
   docker-compose up -d
   ```

### URLs dos Payment Processors

Após subir os containers, os Payment Processors estarão disponíveis em:

- **Default:** `http://payment-processor-default:8080` (interno) / `http://localhost:8001` (externo)
- **Fallback:** `http://payment-processor-fallback:8080` (interno) / `http://localhost:8002` (externo)

### Variáveis de Ambiente

O docker-compose.yml foi configurado com as seguintes variáveis de ambiente:

```yaml
environment:
  - PROCESSOR_DEFAULT_URL=http://payment-processor-default:8080
  - PROCESSOR_FALLBACK_URL=http://payment-processor-fallback:8080
```

### Configuração de Rede

- **Rede externa:** `payment-processor` (criada pelos containers dos payment processors)
- **Rede interna:** `backend` (para comunicação entre nginx e apis)

### Fluxo de Processamento

1. **Tentativa no Payment Processor Default** primeiro (menor taxa)
2. **Fallback automático** para o Payment Processor Fallback em caso de falha
3. **Logs detalhados** de cada tentativa e resultado

### Endpoints Disponíveis

- `POST /payments` - Processa pagamentos
- `GET /payments-summary` - Resumo dos pagamentos processados
- `GET /` - Health check básico

### Teste Local

Use o arquivo `rest.http` para testar os endpoints:

```http
@baseUrl = http://localhost:9999

# Health check
GET {{baseUrl}}/

# Processar pagamento
POST {{baseUrl}}/payments
Content-Type: application/json

{
    "correlationId": "4a7901b8-7d26-4d9d-aa19-4dc1c7cf60b3",
    "amount": 19.90
}

# Consultar resumo
GET {{baseUrl}}/payments-summary?from=2025-07-01T00:00:00Z&to=2025-07-21T23:59:59Z
```

### Recursos Configurados

- **CPU Total:** 1.5 unidades (0.65 + 0.65 + 0.10 + 0.10)
- **Memória Total:** 350MB (125MB + 125MB + 32MB + 68MB)
