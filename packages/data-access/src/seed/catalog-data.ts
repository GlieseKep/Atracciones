import type { AttractionDetails, Rating } from '@atracciones/domain';
import { Money } from '@atracciones/domain';

/**
 * Catálogo inicial (mismos identificadores y contenido que el catálogo de demostración del frontend).
 * Fotografías de Wikimedia Commons (CC BY / CC BY-SA / CC0 / dominio público).
 */
const commons = (path: string, file: string) => `https://upload.wikimedia.org/wikipedia/commons/thumb/${path}/${file}/1280px-${file}`;

const loc = (address: string, latitude: number, longitude: number, city = 'Quito') => ({
  address, city, country: 'EC', latitude, longitude, type: 'MEETING_POINT',
});

const QUITO_TOURS = { id: 1001, name: 'Quito Tours (ejemplo)' };
const ANDES_TRIPS = { id: 1002, name: 'Andes Trips (ejemplo)' };

export interface SeedAttraction {
  id: string;
  rating: Rating;
  details: AttractionDetails;
}

export const SEED_ATTRACTIONS: SeedAttraction[] = [
  {
    id: 'a1b2c3d4-0001-4000-8000-000000000001',
    rating: { numberOfReviews: 1250, score: 4.6 },
    details: {
      name: 'TelefériQo: subida al volcán Pichincha con vistas de Quito',
      longDescription:
        'Sube en telecabina desde el borde occidental de Quito hasta Cruz Loma, a más de 3.900 metros sobre el nivel del mar. En apenas unos minutos pasarás de la ciudad a los páramos andinos, con vistas de la capital y, en días despejados, de los volcanes Cotopaxi, Cayambe y Antisana. Arriba podrás caminar por senderos, tomar un canelazo caliente y fotografiar el atardecer sobre el valle.',
      duration: 'PT2H',
      price: new Money('USD', 9.5),
      categories: ['Aventura', 'Naturaleza'],
      badges: ['Más vendido'],
      locations: [loc('Av. Occidental y La Gasca', -0.1925, -78.5195)],
      photoUrls: [
        commons('9/9b', 'Gondolas_of_the_TeleferiQo_in_Quito%2C_Ecuador.jpg'),
        commons('3/30', 'Top_station_of_the_TeleferiQo_in_Quito%2C_Ecuador.jpg'),
        commons('3/30', 'Curiquingue%2C_cerca_de_la_estaci%C3%B3n_del_Telef%C3%A9riQo.jpg'),
      ],
      operator: QUITO_TOURS,
      productType: 'SINGLE_TICKET',
      includes: ['Entrada'],
      supportedLanguages: ['es', 'en'],
      freeCancellation: true,
    },
  },
  {
    id: 'a1b2c3d4-0002-4000-8000-000000000002',
    rating: { numberOfReviews: 840, score: 4.4 },
    details: {
      name: 'Ciudad Mitad del Mundo: visita guiada a la línea ecuatorial',
      longDescription:
        'Recorre con un guía local el monumento a la línea ecuatorial en San Antonio de Pichincha. Conoce la historia de la misión geodésica franco-española, visita los museos etnográficos y planetario, y toma la clásica foto con un pie en cada hemisferio. Incluye traslado desde Quito y entrada al complejo.',
      duration: 'PT3H',
      price: new Money('USD', 25),
      categories: ['Cultura'],
      badges: [],
      locations: [loc('Av. Manuel Córdova Galarza', -0.0022, -78.4558, 'San Antonio de Pichincha')],
      photoUrls: [
        commons('a/ae', 'Mitad_del_Mundo%2C_Quito%2C_Ecuador%2C_2015-07-22%2C_DD_02.JPG'),
        commons('4/48', 'Mitad_del_Mundo%2C_Quito%2C_Ecuador%2C_2015-07-22%2C_DD_04.JPG'),
        commons('8/8e', 'Mitad_del_Mundo%2C_Quito%2C_Ecuador%2C_2015-07-22%2C_DD_22.JPG'),
      ],
      operator: QUITO_TOURS,
      productType: 'GUIDED_TOUR',
      includes: ['Guía', 'Transporte', 'Entrada'],
      supportedLanguages: ['es', 'en'],
      freeCancellation: true,
    },
  },
  {
    id: 'a1b2c3d4-0003-4000-8000-000000000003',
    rating: { numberOfReviews: 310, score: 4.8 },
    details: {
      name: 'Parque Nacional Cotopaxi: excursión de día completo desde Quito',
      longDescription:
        'Viaja por la Avenida de los Volcanes hasta el Parque Nacional Cotopaxi. Camina alrededor de la laguna de Limpiopungo, observa caballos salvajes y cóndores andinos y, si el clima lo permite, asciende hasta el refugio José Rivas a 4.864 metros. Un guía de montaña certificado te acompaña durante toda la jornada.',
      duration: 'P1D',
      price: new Money('USD', 75),
      categories: ['Aventura', 'Naturaleza'],
      badges: ['Nuevo'],
      locations: [loc('Panamericana Sur km 45', -0.6844, -78.4378, 'Latacunga')],
      photoUrls: [commons('d/d9', 'Cotopaxi_01.jpg'), commons('0/07', 'Cotopaxi_%2837399959476%29.jpg'), commons('b/b2', 'Cotopaxi_02.jpg')],
      operator: ANDES_TRIPS,
      productType: 'PACKAGE',
      includes: ['Guía', 'Transporte'],
      supportedLanguages: ['es'],
      freeCancellation: true,
    },
  },
  {
    id: 'de000000-0004-4000-8000-000000000004',
    rating: { numberOfReviews: 562, score: 4.9 },
    details: {
      name: 'Laguna Quilotoa: caminata por el cráter y comunidades andinas',
      longDescription:
        'Descubre la laguna de aguas turquesa que llena el cráter del volcán Quilotoa. Caminarás por el borde del cráter, descenderás hasta la orilla y visitarás talleres de pintura indígena de Tigua. Almuerzo típico incluido en una comunidad local.',
      duration: 'PT10H',
      price: new Money('USD', 89),
      categories: ['Naturaleza', 'Aventura'],
      badges: ['Probabilidad de agotarse'],
      locations: [loc('Mirador Shalalá, Quilotoa', -0.8584, -78.9047, 'Latacunga')],
      photoUrls: [
        commons('6/63', 'Quilotoa._Volc%C3%A1n_-_Laguna_Ecuador_%282%29.jpg'),
        commons('3/36', 'Quilotoa._Volc%C3%A1n_-_Laguna_Ecuador_%285%29.jpg'),
        commons('a/a7', 'Quilotoa._Volc%C3%A1n_-_Laguna_Ecuador_%287%29.jpg'),
      ],
      operator: ANDES_TRIPS,
      productType: 'PACKAGE',
      includes: ['Guía', 'Transporte', 'Almuerzo'],
      supportedLanguages: ['es', 'en'],
      freeCancellation: true,
    },
  },
  {
    id: 'de000000-0005-4000-8000-000000000005',
    rating: { numberOfReviews: 978, score: 4.7 },
    details: {
      name: 'Centro Histórico de Quito de noche: tour a pie con leyendas',
      longDescription:
        'El primer Patrimonio Cultural de la Humanidad de la UNESCO se transforma de noche. Recorre la Plaza Grande iluminada, iglesias barrocas y callejones coloniales mientras tu guía narra leyendas quiteñas. Termina con una degustación de chocolate ecuatoriano.',
      duration: 'PT2H30M',
      price: new Money('USD', 22),
      categories: ['Cultura', 'Gastronomía'],
      badges: ['Más vendido'],
      locations: [loc('Plaza de la Independencia', -0.2201, -78.5123)],
      photoUrls: [
        commons('a/a5', 'Plaza_de_la_Independencia_%28Quito%29_Historic_Center_at_night_%28El_Centro_Hist%C3%B3rico_de_Quito%29_pic.aa.jpg'),
        commons('7/7b', 'Plaza_de_la_Independencia_%28Quito%29_Historic_Center_at_night_%28El_Centro_Hist%C3%B3rico_de_Quito%29_pic.g1.jpg'),
      ],
      operator: QUITO_TOURS,
      productType: 'GUIDED_TOUR',
      includes: ['Guía', 'Degustación'],
      supportedLanguages: ['es', 'en'],
      freeCancellation: true,
    },
  },
  {
    id: 'de000000-0006-4000-8000-000000000006',
    rating: { numberOfReviews: 411, score: 4.5 },
    details: {
      name: 'Basílica del Voto Nacional: subida a las torres y gárgolas',
      longDescription:
        'Sube a las torres de la mayor iglesia neogótica de América y descubre sus gárgolas inspiradas en la fauna ecuatoriana: tortugas, iguanas y armadillos. Desde el mirador del reloj tendrás una de las mejores vistas del centro de Quito y el Panecillo.',
      duration: 'PT1H30M',
      price: new Money('USD', 12),
      categories: ['Cultura'],
      badges: [],
      locations: [loc('Calle Venezuela y Carchi', -0.2149, -78.5072)],
      photoUrls: [
        commons('5/5a', 'Bas%C3%ADlica_del_Voto_Nacional%2C_Quito_-_4.jpg'),
        commons('c/c3', 'Vista_noroccidental._Bas%C3%ADlica_del_Voto_Nacional%2C_Quito_-_Ecuador.jpg'),
        commons('2/29', 'Bas%C3%ADlica_del_Voto_Nacional%2C_Quito_-_5.jpg'),
      ],
      operator: QUITO_TOURS,
      productType: 'SINGLE_TICKET',
      includes: ['Entrada'],
      supportedLanguages: ['es'],
      freeCancellation: false,
    },
  },
  {
    id: 'de000000-0007-4000-8000-000000000007',
    rating: { numberOfReviews: 689, score: 4.8 },
    details: {
      name: 'Bosque nublado de Mindo: cascadas, colibríes y tarabita',
      longDescription:
        'Escápate al bosque nublado de Mindo, uno de los lugares con mayor diversidad de aves del planeta. Cruza el valle en tarabita, camina hasta la ruta de las cascadas, observa colibríes en un jardín de néctar y visita una pequeña fábrica de chocolate artesanal.',
      duration: 'PT9H',
      price: new Money('USD', 65),
      categories: ['Naturaleza', 'Aventura'],
      badges: [],
      locations: [loc('Vía Mindo – Tarabita', -0.0517, -78.7746, 'Mindo')],
      photoUrls: [commons('e/e4', 'Mindo-Cloud-Forest-03.jpg'), commons('6/69', 'Mindo-Cloud-Forest-06.jpg')],
      operator: ANDES_TRIPS,
      productType: 'PACKAGE',
      includes: ['Guía', 'Transporte', 'Almuerzo', 'Entrada'],
      supportedLanguages: ['es', 'en'],
      freeCancellation: true,
    },
  },
  {
    id: 'de000000-0008-4000-8000-000000000008',
    rating: { numberOfReviews: 254, score: 4.6 },
    details: {
      name: 'Baños: ruta de las cascadas y Pailón del Diablo',
      longDescription:
        'Recorre la Ruta de las Cascadas entre Baños y Río Verde. Cruza puentes colgantes, siente la fuerza del Pailón del Diablo desde sus miradores y opcionalmente vuela en canopy sobre el cañón del río Pastaza.',
      duration: 'PT6H',
      price: new Money('USD', 45),
      categories: ['Aventura', 'Naturaleza'],
      badges: ['Probabilidad de agotarse'],
      locations: [loc('Río Verde, vía Baños – Puyo', -1.4019, -78.2969, 'Baños de Agua Santa')],
      photoUrls: [
        commons('8/84', 'The_base_of_Pail%C3%B3n_del_Diablo.jpg'),
        commons('5/56', 'R%C3%ADo_Verde_Pail%C3%B3n_del_Diablo_trail_1.jpg'),
        commons('0/0e', 'R%C3%ADo_Verde_Pail%C3%B3n_del_Diablo_trail_2.jpg'),
      ],
      operator: ANDES_TRIPS,
      productType: 'GUIDED_TOUR',
      includes: ['Guía', 'Transporte'],
      supportedLanguages: ['es'],
      freeCancellation: true,
    },
  },
  {
    id: 'de000000-0009-4000-8000-000000000009',
    rating: { numberOfReviews: 147, score: 4.7 },
    details: {
      name: 'Volcán Chimborazo: el punto más cercano al sol',
      longDescription:
        'Visita la Reserva de Producción de Fauna Chimborazo, hogar de vicuñas en libertad. Llega hasta el refugio Carrel y camina hacia el refugio Whymper a 5.000 metros, en el volcán cuya cumbre es el punto de la Tierra más alejado de su centro.',
      duration: 'P1D',
      price: new Money('USD', 95),
      categories: ['Aventura', 'Naturaleza'],
      badges: [],
      locations: [loc('Reserva de Producción de Fauna Chimborazo', -1.4693, -78.8169, 'Riobamba')],
      photoUrls: [
        commons('9/9c', 'Chimborazo_04.jpg'),
        commons('7/7c', 'Vicu%C3%B1a_-_Chimborazo%2C_Ecuador.jpg'),
        commons('8/8f', 'Chimborazo_05.jpg'),
      ],
      operator: ANDES_TRIPS,
      productType: 'PACKAGE',
      includes: ['Guía', 'Transporte', 'Almuerzo'],
      supportedLanguages: ['es', 'en'],
      freeCancellation: false,
    },
  },
  {
    id: 'de000000-0010-4000-8000-000000000010',
    rating: { numberOfReviews: 523, score: 4.5 },
    details: {
      name: 'Quito panorámico: El Panecillo, miradores y barrio La Ronda',
      longDescription:
        'Tour en vehículo privado por los mejores miradores de Quito: la Virgen del Panecillo, el mirador de Guápulo y el parque Itchimbía. Termina con un paseo por la calle colonial de La Ronda y una bebida tradicional.',
      duration: 'PT4H',
      price: new Money('USD', 35),
      categories: ['Cultura', 'Fotografía'],
      badges: [],
      locations: [loc('El Panecillo', -0.2296, -78.5183)],
      photoUrls: [commons('4/44', 'Quito_as_from_panecillo_Basilica.jpg')],
      operator: QUITO_TOURS,
      productType: 'GUIDED_TOUR',
      includes: ['Guía', 'Transporte'],
      supportedLanguages: ['es', 'en'],
      freeCancellation: true,
    },
  },
];
