## ESTADO
- [x] 1 Base: solución, modelo, migraciones, JWT y roles, seeder
- [x] 2 Activos, hoja de vida, planes, lecturas, órdenes de trabajo
- [x] 3 Alertas al 90 % con pruebas
- [x] 4 Indicadores, tablero y pruebas

Decisiones:
- Dev: appsettings.Development.json (gitignored) lleva conexión, Jwt:Key y Seed:Password; API http://localhost:5282, Web http://localhost:5235.
- Usuarios seed: admin / jefe / comercial (misma clave de Seed:Password).
- Línea base de un plan sin mtto. previo: 0 km, 0 h, fecha de puesta en servicio.
- Falla = OT correctiva no cancelada; reparación = correctiva cerrada (cierre − apertura); tiempo operativo = periodo − caídas. Sin fallas, disponibilidad = 100 %.
- Estado del activo derivado de OT activas (correctiva → FueraDeServicio, preventiva → EnMantenimiento).
- Sesión Web en memoria del circuito (se pierde al recargar). Cierre de OT usa la última lectura.
- Seeder usa fechas fijas; las alertas por días dependen de la fecha actual.
