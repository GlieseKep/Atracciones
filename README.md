# AEROCACHE Airlines - Microservicio REST de Vuelos y Portal Web

Sistema integral de microservicio centralizado para búsqueda, ofertas, bloqueo de cupos (hold), reservas, emisión de tickets y postventa de vuelos domésticos en Ecuador (**Quito UIO, Guayaquil GYE y Cuenca CUE**). 

Implementa al 100% el contrato estricto OpenAPI [`vuelos-openapi.yaml`](file:///C:/Users/default.LAPTOP-N0CGE918/Downloads/vuelos-openapi.yaml) con arquitectura en capas para **Visual Studio 2022 (.NET 9)**, interfaz web inspirada en **LATAM Airlines** y despliegue automatizado con **Docker y Docker Compose**.

---

## 🛫 Red Aérea Exclusiva (Ecuador Doméstico)
- **Quito (UIO)** - Aeropuerto Internacional Mariscal Sucre
- **Guayaquil (GYE)** - Aeropuerto Internacional José Joaquín de Olmedo
- **Cuenca (CUE)** - Aeropuerto Mariscal La Mar

Flota operada: **Airbus A320-200** con tarifas **Light, Plus y Top**.

---

## 🏛 Arquitectura de la Solución (Visual Studio 2022)

```
Aerocache/
├── Aerocache.sln                         # Solución compatible con Visual Studio 2022
├── Aerocache.slnx                        # Formato moderno de solución VS 2022 v17.10+
├── docker-compose.yml                    # Orquestador Docker multi-contenedor
├── Dockerfile.api                        # Imagen Docker del microservicio .NET 9
│
├── Aerocache.API/                        # Web API REST (Solo REST)
│   ├── Controllers/                      # Search, Offers, Bookings, PostSale, CheckIn, Flights, Webhooks
│   ├── Middleware/                       # RFC 7807 ProblemDetailsExceptionMiddleware
│   └── Program.cs                        # Configuración CORS, Swagger y Semillado automático
│
├── Aerocache.Business/                   # Lógica de Negocio y Reglas
│   ├── DTOs/                             # Modelos estrictos según vuelos-openapi.yaml
│   ├── Exceptions/                       # Jerarquía de excepciones de negocio
│   └── Services/                         # Servicios de Búsqueda, Hold, Emisión, Postventa, Check-in
│
├── Aerocache.DataAccess/                 # Acceso a Datos y Persistencia
│   ├── Context/                          # AerocacheDbContext (SQLite portable aerocache.db)
│   ├── Entities/                         # Flight, CabinFare, Seat, Hold, Booking, Passenger, Ticket
│   └── Seed/                             # AerocacheDataSeeder (Vuelos diarios UIO, GYE, CUE)
│
├── Aerocache.DataManagement/             # Patrón Repositorio y Unit of Work
│   ├── Interfaces/                       # IGenericRepository, IUnitOfWork
│   └── Repositories/                     # GenericRepository, UnitOfWork
│
└── aerocache-web/                        # Portal Web Visual (Inspirado en LATAM Airlines)
    ├── Dockerfile                        # Imagen de frontend con servidor Nginx
    ├── nginx.conf                        # Reverse proxy hacia la API
    └── src/
        ├── components/                   # Navbar, Buscador, Tarjetas de Vuelo, Asientos, Pases de Abordar
        └── pages/                        # Comprar Vuelos, Mis Viajes (Postventa), Estado de Vuelo
```

---

## 🚀 Cómo Ejecutar el Proyecto

### Opción 1: Con Visual Studio 2022 o .NET CLI (Recomendado para desarrollo)

1. **Abrir en Visual Studio 2022**:
   - Haz doble clic en [`Aerocache.sln`](file:///C:/Users/default.LAPTOP-N0CGE918/.gemini/antigravity/scratch/Aerocache/Aerocache.sln).
   - Establece `Aerocache.API` como proyecto de inicio y presiona **F5** o **Ctrl+F5**.
   - O por terminal:
     ```powershell
     cd C:\Users\default.LAPTOP-N0CGE918\.gemini\antigravity\scratch\Aerocache
     dotnet run --project Aerocache.API\Aerocache.API.csproj --urls "http://localhost:5200"
     ```
   - Swagger estará disponible en: [http://localhost:5200/swagger](http://localhost:5200/swagger)

2. **Ejecutar el Frontend Web**:
   ```powershell
   cd C:\Users\default.LAPTOP-N0CGE918\.gemini\antigravity\scratch\Aerocache\aerocache-web
   npm run dev
   ```
   - Abrir en el navegador: [http://localhost:5173](http://localhost:5173)

---

### Opción 2: Con Docker y Docker Compose

Levanta tanto la API como el frontend con un solo comando:
```powershell
cd C:\Users\default.LAPTOP-N0CGE918\.gemini\antigravity\scratch\Aerocache
docker compose up --build -d
```
- **Portal Web AEROCACHE**: [http://localhost:3000](http://localhost:3000)
- **API REST & Swagger**: [http://localhost:5200/swagger](http://localhost:5200/swagger)

---

## 📡 Endpoints Implementados (100% Contrato OpenAPI)

- `POST /search`: Búsqueda de vuelos domésticos (UIO, GYE, CUE).
- `GET /offers/{offerId}/seatmap`: Mapa visual de asientos por cabina y segmento.
- `POST /offers/hold`: Bloqueo de cupos por 15 minutos (precio congelado).
- `GET /offers/hold/{holdId}`: Estado y tiempo restante del hold.
- `DELETE /offers/hold/{holdId}`: Liberación anticipada del hold.
- `GET /bookings`: Listado de reservas por PNR o estado.
- `POST /bookings`: Creación de reserva con referencia de pago y emisión de tickets.
- `GET /bookings/{id}`: Detalle completo de la reserva.
- `GET /bookings/{id}/tickets`: Consulta de tickets electrónicos (045-XXXXXXXXXX).
- `GET /bookings/{id}/baggage-options`: Consulta de maletas adicionales.
- `POST /bookings/{id}/baggage`: Compra de maleta extra post-emisión.
- `POST /bookings/{id}/date-change/search`: Cotización de cambio de fecha.
- `POST /bookings/{id}/date-change`: Confirmación de reprogramación de vuelo.
- `GET /bookings/{id}/cancellation-quote`: Cotización formal de reembolso.
- `POST /bookings/{id}/cancel`: Cancelación definitiva de reserva.
- `POST /bookings/{id}/check-in`: Check-in web con asignación de asientos.
- `GET /bookings/{id}/boarding-passes`: Tarjetas de embarque digitales con QR.
- `GET /flights/{flightNumber}/status`: Estado operativo en tiempo real.
- `GET /webhooks`, `POST /webhooks`, `DELETE /webhooks/{id}`: Suscripción a eventos.
