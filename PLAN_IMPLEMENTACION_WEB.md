# Plan de implementación de AtraccionesService-web

> Este documento define la estructura, diseño visual, componentes, flujo de usuario y arquitectura de la interfaz web para presentar atracciones fotogénicas de Ecuador. No modifica el contrato API ni implementa código.

## 1. Objetivo

Crear un portal web atractivo, responsive y fácil de navegar para explorar atracciones turísticas de Ecuador. La interfaz debe:

- Destacar paisajes, experiencias y detalles fotogénicos.
- Permitir buscar y filtrar atracciones.
- Mostrar información clara, visual y verificable.
- Integrarse con OAuth2, catálogo, disponibilidad, reservas, compras directas, pedidos y pagos simulados.
- Mantener una experiencia coherente entre desktop, tablet y móvil.
- Usar una paleta cálida con énfasis en colores rosados y tonos tierra.

La referencia del repositorio externo se utiliza únicamente para orientar la estructura general de componentes, páginas y servicio de frontend. No se copiarán sus textos, rutas, datos ni diseño específico.

## 2. Tecnologías propuestas

### Frontend

- React 19.
- TypeScript.
- Vite.
- Vite React Plugin.
- React Router.
- Axios o Fetch API para la comunicación HTTP.
- Zustand o Context API para estado global.
- React Hook Form para formularios.
- Zod para validación de formularios y DTOs.
- Tailwind CSS 3.x o CSS Modules.
- Lucide React para iconografía.
- Framer Motion para animaciones leves.

### Recomendación de arquitectura

Se recomienda usar React + TypeScript + Vite, con Tailwind CSS para la apariencia y un conjunto pequeño de componentes reutilizables. Evitar librerías visuales grandes que limiten el estilo personalizado.

## 3. Estructura propuesta

```text
AtraccionesService-web/
├── public/
│   ├── images/
│   │   ├── destinations/
│   │   ├── attractions/
│   │   └── brand/
│   ├── icons/
│   └── favicon.svg
├── src/
│   ├── api/
│   │   ├── client.ts
│   │   ├── auth.ts
│   │   ├── attractions.ts
│   │   ├── availability.ts
│   │   ├── reservations.ts
│   │   ├── customers.ts
│   │   ├── purchases.ts
│   │   ├── orders.ts
│   │   ├── payments.ts
│   │   └── admin.ts
│   ├── app/
│   │   ├── router.tsx
│   │   ├── routes.ts
│   │   └── providers.tsx
│   ├── components/
│   │   ├── common/
│   │   │   ├── Button/
│   │   │   ├── Input/
│   │   │   ├── Select/
│   │   │   ├── Modal/
│   │   │   ├── Badge/
│   │   │   ├── Rating/
│   │   │   ├── IconButton/
│   │   │   └── LoadingSpinner/
│   │   ├── layout/
│   │   │   ├── Navbar/
│   │   │   ├── Footer/
│   │   │   ├── MobileMenu/
│   │   │   └── PageHero/
│   │   ├── attractions/
│   │   │   ├── AttractionCard/
│   │   │   ├── AttractionGallery/
│   │   │   ├── AttractionDetails/
│   │   │   ├── AttractionMap/
│   │   │   └── AttractionFilters/
│   │   ├── search/
│   │   │   ├── SearchBar/
│   │   │   ├── SearchSummary/
│   │   │   └── SearchResults/
│   │   ├── reservation/
│   │   │   ├── ReservationForm/
│   │   │   ├── AvailabilityCalendar/
│   │   │   ├── AvailabilitySelector/
│   │   │   └── ReservationSummary/
│   │   ├── purchase/
│   │   │   ├── PurchaseForm/
│   │   │   ├── PaymentMethodSelector/
│   │   │   └── PurchaseSummary/
│   │   ├── profile/
│   │   │   ├── ProfileCard/
│   │   │   ├── BillingForm/
│   │   │   └── ReservationHistory/
│   │   └── ui/
│   │       ├── SectionHeader/
│   │       ├── DestinationChip/
│   │       └── StoryCard/
│   ├── pages/
│   │   ├── HomePage/
│   │   ├── AttractionsPage/
│   │   ├── AttractionDetailPage/
│   │   ├── SearchPage/
│   │   ├── ReservationPage/
│   │   ├── PurchasePage/
│   │   ├── OrderPage/
│   │   ├── ProfilePage/
│   │   ├── AuthCallbackPage/
│   │   ├── AdminDashboardPage/
│   │   ├── AdminCatalogPage/
│   │   ├── AdminAvailabilityPage/
│   │   ├── AdminReservationsPage/
│   │   ├── AdminOrdersPage/
│   │   ├── AdminPaymentsPage/
│   │   ├── AdminCustomersPage/
│   │   ├── AdminUsersPage/
│   │   ├── AdminReportsPage/
│   │   └── NotFoundPage/
│   ├── hooks/
│   │   ├── useAuth.ts
│   │   ├── useAttractions.ts
│   │   ├── useReservations.ts
│   │   ├── usePurchase.ts
│   │   └── usePagination.ts
│   ├── stores/
│   │   ├── authStore.ts
│   │   ├── purchaseStore.ts
│   │   └── uiStore.ts
│   ├── context/
│   │   └── AppContext.tsx
│   ├── features/
│   │   ├── auth/
│   │   ├── attractions/
│   │   ├── reservations/
│   │   └── purchase/
│   ├── types/
│   │   ├── api.ts
│   │   ├── attraction.ts
│   │   ├── reservation.ts
│   │   ├── purchase.ts
│   │   ├── order.ts
│   │   └── payment.ts
│   ├── utils/
│   │   ├── formatters.ts
│   │   ├── validation.ts
│   │   ├── routes.ts
│   │   ├── dates.ts
│   │   └── api.ts
│   ├── styles/
│   │   ├── globals.css
│   │   ├── tokens.css
│   │   └── components.css
│   ├── config/
│   │   └── env.ts
│   ├── assets/
│   │   └── images/
│   ├── App.tsx
│   ├── main.tsx
│   └── vite-env.d.ts
├── index.html
├── package.json
├── tsconfig.json
├── tsconfig.node.json
├── vite.config.ts
├── eslint.config.js
├── .env.example
└── README.md
```

## 4. Diseño visual

### 4.1 Temática

El sitio se presentará como una plataforma de descubrimiento visual de Ecuador, combinando:

- Paisajes volcánicos.
- Lagos y montañas.
- Playas y bosques.
- Cascadas y senderos.
- Ciudad colonial, cultura y gastronomía.
- Exploración en grupos o en pareja.

La marca debe transmitir belleza, variedad, naturaleza, cercanía y experiencias auténticas.

### 4.2 Paleta de colores

| Uso | Color | Descripción |
|---|---|---|
| Fondo principal | `#FFF9F7` | Crema rosada muy claro. |
| Fondo secundario | `#FBE8E8` | Rosa claro cálido. |
| Color principal | `#E85A8A` | Rosa coral intenso. |
| Color secundario | `#C94D6B` | Rosa vino medio. |
| Acento cálido | `#F4B183` | Melocotón suave. |
| Acento dorado | `#D7A65C` | Amarillo-miel para detalles premium. |
| Texto principal | `#332326` | Marrón oscuro. |
| Texto secundario | `#6B4D50` | Marrón rosado. |
| Fondo oscuro | `#2A1B1D` | Café casi negro. |
| Blanco | `#FFFFFF` | Utilizado para contraste. |
| Error | `#B93D4A` | Rojo coral para alertas. |
| Éxito | `#3A7F64` | Verde similar a paisajes vegetales. |

La paleta debe usarse como base, pero la fotografía y los grafismos también aportarán color. No se deben usar sólo tonos rosas; se introducirán neutros cálidos y verdes suaves para equilibrar la interfaz.

### 4.3 Tipografía

- Título principal: serif elegante, por ejemplo `Cormorant Garamond`, `Playfair Display` o una alternativa local.
- Texto general: sans-serif moderno, por ejemplo `Inter`, `Manrope` o `Poppins`.
- Tamaños responsivos y una línea máxima de lectura ajustada.
- Utilizar texto de alta legibilidad sobre imágenes mediante overlays oscuros.

### 4.4 Espaciado y composición

- Sistema de espaciado basado en 8px.
- Radios de 12px, 20px y 32px.
- Sombras suaves y difusas.
- Imágenes con recortes redondeados.
- Uso de gradients cálidos para crear profundidad.
- Celdas de contenido con elevación sutil en hover.

## 5. Secciones de la página principal

### 5.1 Header

- Logo con nombre de la marca, por ejemplo “Ecuador Aesthetic”.
- Menú principal:
  - Inicio.
  - Atracciones.
  - Destinos.
  - Experiencias.
  - Blog o historias.
  - Mis reservas.
- Botones:
  - Explorar.
  - Iniciar sesión.
  - Carrito.
- Menú móvil con navegación compacta.

### 5.2 Hero

- Imagen grande de una atracción representativa de Ecuador.
- Título inspirador con mensaje turístico.
- Subtítulo orientado a experiencias fotogénicas.
- Botones de búsqueda y exploración.
- Indicadores visuales de ciudades o regiones del país.

### 5.3 Destacados

- Cinco o seis destinos con miniaturas.
- Calificación.
- Descripción breve.
- Badge de categoría.
- Botón “Descubrir”.

### 5.4 Buscador

- Ciudad o región.
- Fecha.
- Tipo de experiencia.
- Presupuesto máximo.
- Idioma o categoría.
- Botón de búsqueda.

### 5.5 Recomendaciones

- Atracciones destacadas por temporada.
- Experiencias para parejas, familia o viajeros fotográficos.
- Listado de atracciones con imagen, precio y calificación.

### 5.6 Sección “Por qué viajar con nosotros”

- Garantía de disponibilidad.
- Recomendaciones locales.
- Reservas seguras.
- Experiencias verificadas.

### 5.7 Testimonios

- Historias reales o representativas de viajantes.
- Fotos pequeñas y estrellas de calificación.

### 5.8 Footer

- Información de marca.
- Contacto.
- Políticas de cancelación.
- Términos de uso.
- Redes sociales.
- Enlaces a soporte y privacidad.

## 6. Flujo de navegación

### 6.1 Inicio

```text
Home
  ├── Buscar atracciones
  ├── Explorar por destino
  ├── Ver experiencias destacadas
  └── Iniciar sesión / perfil
```

### 6.2 Exploración

```text
Atracciones
  ├── Filtros
  ├── Lista de resultados
  ├── Paginación
  └── Detalle de atracción
```

### 6.3 Reserva

```text
Detalle de atracción
  ├── Select disponibilidad
  ├── Completar datos de reserva
  ├── Revisar precio
  ├── Confirmar reserva
  └── Ver detalle de reserva
```

### 6.4 Compra directa

```text
Compra directa
  ├── Seleccionar atracción, fecha y horario
  ├── Revisar cantidad y precio
  ├── Datos de facturación
  ├── Método de pago simulado
  └── Pedido confirmado
```

### 6.5 Perfil

```text
Perfil
  ├── Datos personales
  ├── Datos de facturación
  ├── Reservas
  ├── Pedidos
  └── Historial y favoritos

### 6.6 Administración

```text
Área administrativa
  ├── Dashboard y reportes autorizados
  ├── Catálogo y disponibilidad
  ├── Reservas y pedidos
  ├── Pagos y reembolsos simulados
  ├── Clientes y estado de perfiles
  └── Usuarios y roles locales (solo permiso autorizado)
```
```

## 7. Páginas propuestas

### 7.1 HomePage

- Hero.
- Buscador.
- Destinos destacables.
- Atracciones populares.
- Experiencias recomendadas.
- Opiniones.
- Banner de registro / acceso OAuth2.

### 7.2 AttractionsPage

- Filtros por ciudad, categoría, precio y disponibilidad.
- Listado con tarjetas.
- Ordenamiento.
- Paginación.
- Estado vacío.
- Resultados por atracción.

### 7.3 AttractionDetailPage

- Galería de imágenes.
- Nombre, ubicación y valoración.
- Descripción detallada.
- Servicios incluidos.
- Categorías e insignias.
- Precio.
- Disponibilidad.
- Botones para seleccionar fecha y reservar.
- Recomendaciones relacionadas.

### 7.4 SearchPage

- Búsqueda avanzada.
- Filtros persistentes.
- Resultados y mensaje de cero resultados.
- Indicación de distancia o categoría.

### 7.5 ReservationPage

- Selección de fecha y horario.
- Cantidad de tickets.
- Datos del cliente.
- Reglas de cancelación.
- Resumen de precio.
- Confirmación de reserva.

### 7.6 PurchasePage

- Selección de fecha, horario y cantidad.
- Confirmación del precio calculado por el servidor.
- Datos de facturación según el método de pago.
- Método de pago simulado.
- Resumen final y enlace al pedido.

### 7.7 OrderPage

- Estado del pedido.
- Resumen de pago.
- Código de seguimiento interno.
- Historial de eventos.
- Botón de cancelar pedido cuando esté permitido.

### 7.9 ProfilePage

- Información personal.
- Dirección de facturación.
- Reservas.
- Pedidos.
- Pagos simulados.
- Configuración de perfil.

### 7.10 AuthCallbackPage

- Procesamiento de respuesta OAuth2.
- Procesamiento del retorno del issuer y eliminación de estado temporal.
- El access token se mantiene en memoria de la aplicación siempre que el cliente OAuth2 lo permita; no se persiste en `localStorage` ni en `sessionStorage`.
- Redirección segura.

### 7.11 Área administrativa

- `AdminDashboardPage`: indicadores agregados y enlaces según permisos.
- `AdminCatalogPage`: crear, editar, publicar/desactivar atracciones.
- `AdminAvailabilityPage`: consultar cupos y registrar ajustes auditados.
- `AdminReservationsPage`: buscar y resolver reservas.
- `AdminOrdersPage`: consultar pedidos y ejecutar transiciones permitidas.
- `AdminPaymentsPage`: consultar estado de simulaciones y solicitar reembolso simulado; nunca muestra datos de tarjeta.
- `AdminCustomersPage`: consultar información necesaria para soporte.
- `AdminUsersPage`: activar/desactivar perfiles locales y asignar roles cuando la política lo permita.
- `AdminReportsPage`: consultar métricas agregadas con paginación.

Las rutas y permisos de administración solo se habilitan cuando el contrato de API y sus scopes hayan sido aprobados. La ocultación de controles en el frontend no reemplaza autorización del servidor.

## 8. Estado global

### AuthStore

- Usuario autenticado.
- Claims de acceso.
- Estado de autenticación.
- Estado de sesión y claims no sensibles requeridos por la interfaz; el token no se persiste en almacenamiento web durable.
- Acciones de login, logout y callback.

### PurchaseStore

- Atracción, fecha, horario y cantidad seleccionados.
- Precio calculado por el servidor.
- Estado de disponibilidad y carga.
- Resultado de la compra y enlace al pedido.

### UiStore

- Tema visual.
- Modal activo.
- Modo móvil.
- Notificaciones.
- Carga global.

## 9. Comunicación con API

### `api/client.ts`

Debe centralizar:

- URL base.
- Cabeceras.
- Login OAuth2.
- Interceptor de tokens.
- Interceptor de errores.
- Generación/reutilización de `Idempotency-Key` por intento lógico de mutación, conservándola al reintentar la misma solicitud; nunca reutilizarla para otra operación o payload.
- Deserialización de respuestas.

### `api/auth.ts`

Debe exponer:

- Registro de usuario.
- Inicio de sesión por OAuth2.
- Logout.
- Consulta de perfil.
- Callback de autenticación.

### `api/attractions.ts`

Debe exponer:

- Búsqueda.
- Listado.
- Detalle.
- Disponibilidad.
- Creación, actualización y eliminación cuando corresponda.

### API de reserva y ecommerce

- `reservations.ts` para reservas.
- `purchases.ts` para compras directas.
- `orders.ts` para pedidos y eventos.
- `payments.ts` para pagos simulados.
- `admin.ts` para endpoints administrativos aprobados, con funciones separadas por permiso.

El frontend nunca debe almacenar contraseñas, credenciales bancarias ni secretos de OAuth2.

## 10. Gestión de autenticación

- El login se realizará mediante OAuth2 Authorization Code con PKCE.
- La aplicación debe usar `oauth2` como proveedor y no implementará un login local ni tokens simulados.
- El frontend guardará solo el estado de sesión no sensible; no guardará access/refresh tokens en `localStorage` o `sessionStorage`. La sesión y renovación siguen las capacidades del issuer OAuth2.
- Las rutas privadas deben verificar que haya una sesión válida.
- El callback OAuth2 debe procesar el `code` y obtener el token del proveedor.
- Una sesión inválida debe redirigir al inicio externo de autenticación.
- Los scopes deben reflejar las operaciones que exige el API.

## 11. Manejo de errores

Los componentes deben mostrar:

- Error de conexión.
- Token expirado.
- Error de autenticación.
- Error de permiso.
- Recurso no encontrado.
- Error de disponibilidad.
- Error de pago.
- Duplicación de idempotencia.

Los mensajes deben ser amigables y no mostrar detalles internos, stack traces ni secretos.

## 12. Accesibilidad

- Utilizar etiquetas accesibles en todos los formularios.
- Garantizar contraste mínimo de 4.5:1.
- Mantener navegación por teclado.
- Añadir `aria-label` a iconos interactivos.
- Usar `aria-live` para mensajes de éxito o error.
- Mostrar estados claros de foco y hover.
- Garantizar imágenes con texto alternativo descriptivo.
- Usar un orden semántico correcto de encabezados.

## 13. Responsive design

### Mobile

- Header con menú colapsable.
- Buscador vertical.
- Tarjetas de atracción de una columna.
- Botones de acción grandes.
- Formularios con campos de facilidad táctil.

### Tablet

- Dos columnas para catálogo y contenidos relacionados.
- Menú de navegación compacto.
- Buscador en panel secundario o línea de búsqueda.

### Desktop

- Hero amplio y destacado.
- Tres columnas de búsqueda y tarjetas.
- Miniaturas de destino con modelo grid.
- Detalles ampliados de producto.

## 14. Diseño de componentes

### Botones

- Primario: rosa intensivo.
- Secundario: blanco con borde rosado.
- Terciario: fondo cálido transparente.
- Deshabilitado: gris claro.
- Peligro: coral para cancelaciones.

### Tarjetas

- Imagen superior con overlay.
- Título y ubicación.
- Precio y valoraciones.
- Badge de disponibilidad.
- Hover con escala ligera o efecto de elevation.
- Sombra cálida, nunca negra.

### Formularios

- Inputs con borde rosado suave.
- Focus visible con color principal.
- Labels claros y mensajes de ayuda.
- Validación en tiempo real y mensajes de error.

## 15. Animaciones

- La entrada de contenido debe ser suave y breve.
- Las imágenes deben usar fade o zoom ligero.
- Los cards deben responder con elevación al hover.
- No se deben usar animaciones distractoras ni duraciones largas.
- Las animaciones deben reducirse cuando el usuario prefiera menos movimiento.

## 16. Estado de carga y vacío

Cada lista debe mostrar:

- Skeletons para tarjetas y contenido.
- Mensajes de vaciado de resultados.
- Recomendaciones para corregir criterios de búsqueda.
- Botones para volver a intentar la carga.

## 17. Pruebas de frontend

### Pruebas unitarias

- Formateo de precios.
- Validación de fechas y horarios.
- Tradución de errores API.
- Filtros y búsqueda.
- Redirección de rutas protegidas.
- Estado de compra directa.
- Persistencia del usuario.

### Pruebas de componentes

- Navbar.
- Hero.
- Card de atracción.
- Buscador.
- Selector de disponibilidad.
- Formulario de reserva.
- Formulario de compra directa.
- Gestión de perfil.

### Pruebas de integración

- Navegar desde home hacia detalle.
- Buscar filtrando por región.
- Reservar con una fecha disponible.
- Comprar directamente con disponibilidad y precio válidos.
- Simular un pago.
- Ver un pedido y su historial.
- Iniciar sesión OAuth2.
- Probar acceso a una reserva ajena.
- Probar rutas administrativas para usuario sin rol y rol sin scope, confirmando denegación del servidor.
- Probar que controles de administración no habilitan datos o acciones sin permisos y que la interfaz oculta PII no autorizada.

### Pruebas de E2E

- Flujo completo de búsqueda y reserva.
- Flujo completo de compra directa y pago.
- Login y logout OAuth2.
- Flujo de compra directa.
- Gestión del perfil.
- Flujo administrativo por rol: catálogo, disponibilidad, pedido, consulta de pago, reembolso simulado y auditoría visible según permiso.
- Página de error y redirección.

## 18. Fases de implementación

### Fase 1: Preparación del proyecto

1. Crear Vite + React + TypeScript.
2. Configurar ESLint y Prettier.
3. Crear estructura global de carpetas.
4. Instalar Tailwind, iconos y librerías necesarias.
5. Crear estilos de tokens, tipografías y elementos base.
6. Configurar variables de entorno.

### Fase 2: Diseño visual y layout

1. Crear Navbar.
2. Crear Footer.
3. Crear Hero.
4. Crear sección de destinos.
5. Crear sección de atracciones.
6. Crear componentes visuales comunes.
7. Crear diseño responsive.
8. Validar accesibilidad inicial.

### Fase 3: Catálogo

1. Crear página de atracciones.
2. Crear buscador y filtros.
3. Crear resultado paginado.
4. Crear card de atracción.
5. Crear galería y detalle.
6. Crear componentes de disponibilidad.
7. Conectar con API del catálogo.

### Fase 4: Reservas

1. Crear formulario de reserva.
2. Crear selector de fecha y horario.
3. Integrar idempotency.
4. Mostrar costo y detalles.
5. Procesar respuesta y redirección al detalle.
6. Crear historial de reservas.

### Fase 5: Ecommerce

1. Crear formulario de compra directa con selección de fecha, horario y cantidad.
2. Solicitar validación de disponibilidad y precio al servidor.
3. Mostrar resumen de precio y condiciones de compra.
4. Confirmar la compra y generar el pedido una sola vez.
5. Solicitar la simulación de pago sin enviar resultados, intentos ni eventos.
6. Mostrar el pedido y su estado final; permitir reintento solo mediante una nueva operación lógica autorizada.

### Fase 6: Autenticación

1. Configurar OAuth2 con PKCE.
2. Crear callback y almacenamiento seguro de sesión.
3. Crear pantalla de perfil.
4. Proteger rutas privadas.
5. Mostrar estados de sesión y permisos.

### Fase 7: Administración

1. Crear layout y rutas `/admin/*` protegidas por permiso.
2. Añadir vistas de dashboard, catálogo, disponibilidad, reservas, pedidos, pagos, usuarios y reportes.
3. Enlazar cada acción a los endpoints administrativos aprobados y a sus permisos específicos.
4. Mostrar confirmación y motivo para mutaciones sensibles; el servidor registra auditoría.
5. Validar autorización denegada, paginación, estados y errores RFC 7807.

### Fase 8: Optimización y despliegue

1. Optimizar imágenes.
2. Implementar lazy loading.
3. Configurar cache HTTP.
4. Probar en diferentes navegadores.
5. Ejecutar pruebas E2E.
6. Preparar docker y variables de producción.

## 19. Criterios de aceptación

La aplicación frontend estará lista cuando:

1. El portal sea responsive y visualmente coherente.
2. El catálogo y las atracciones se presentan con imágenes de calidad.
3. El buscador filtra por ciudad, categoría, precio y disponibilidad.
4. El detalle incluye fotos, servicios, ubicación y reserva.
5. El usuario puede crear y modificar una reserva.
6. La compra directa valida disponibilidad, cantidad y precio antes de pagar.
7. La compra procesa pagos simulados sin intermediar un carrito.
8. OAuth2 protege las rutas privadas.
9. Los errores API se muestran sin revelar información interna.
10. La aplicación funciona sin conexión a la API mediante estados de carga y errores claros.
11. La navegación funciona con teclado y lectores de pantalla.
12. La interfaz conserva la paleta cálida rosada y la temática Ecuador Aesthetic.

## 20. Riesgos y decisiones pendientes

1. Definir el conjunto final de ciudades y atracciones iniciales.
2. Definir la cantidad de imágenes disponible para cada atracción.
3. Confirmar la política de uso de varias imágenes por reserva.
4. Definir si el catálogo usa búsqueda local o API remota.
5. Confirmar el flujo de autenticación y estrategia de logout.
6. Determinar como se manejan versiones de API y errores locales.
7. Definir si la aplicación tendrá un blog o contenido editorial.
8. Confirmar si habrá favoritos o wishlist.
9. Decidir si se usa caché local para los resultados de búsqueda.
10. Confirmar el estilo final de imagenis, iconos y tipografías.

## 21. Estado actual

La carpeta `AtraccionesService-web` contiene actualmente solamente un archivo de plantilla y no tiene configuración de frontend. El siguiente paso recomendado es iniciar el proyecto Vite, integrar Tailwind y crear primero el diseño del Home y la página de detalle.
