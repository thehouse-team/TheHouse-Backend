// NV-26 — Catálogos públicos de TheHouse: universidades, propiedades y lugares de búsqueda.
// Sin datos personales. Requiere haber ejecutado antes 01-schema.mongodb.js.
//
// ARCHIVO GENERADO desde el código por TheHouse.IntegrationTests/DatabaseScriptTests.cs:
// no se edita a mano (ver scripts/db/README.md para regenerarlo).
//
// Uso:   mongosh "<cadena de conexión>" --file scripts/db/02-catalogs.mongodb.js
// Base:  variable de entorno THEHOUSE_DB (por defecto thehouse_u2).
// Idempotente: solo crea o actualiza lo que falta; nunca borra colecciones, índices ni documentos.
// No contiene cadenas de conexión, usuarios, contraseñas ni datos personales.

const dbName = (typeof process !== "undefined" && process.env.THEHOUSE_DB) || "thehouse_u2";
const target = db.getSiblingDB(dbName);

// Los `_id` de universidad los asigna MongoDB: las propiedades se ligan por código.
function universityId(code) {
  const university = target.universities.findOne({ code: code }, { projection: { _id: 1 } });
  if (university === null) {
    throw new Error(`Falta la universidad ${code}: el catálogo de universidades debe cargarse primero.`);
  }
  return university._id.toHexString();
}

const totals = {};
function upsert(collection, filter, update) {
  const result = target.getCollection(collection).updateOne(filter, update, { upsert: true });
  const entry = totals[collection] || (totals[collection] = { inserted: 0, updated: 0, unchanged: 0 });
  if (result.upsertedCount > 0) {
    entry.inserted += 1;
  } else if (result.modifiedCount > 0) {
    entry.updated += 1;
  } else {
    entry.unchanged += 1;
  }
}

// universities (10)
upsert("universities",
  { code: "UTTT" },
  { $set: { code: "UTTT", name: "Universidad Tecnológica de Tula-Tepeji", city: "Tula de Allende", country: "México", searchKey: "uttt universidad tecnologica de tula tepeji tula de allende", location: { type: "Point", coordinates: [-99.34471, 20.00903] } }, $setOnInsert: { createdAt: ISODate("2026-11-02T00:00:00.000Z") } });
upsert("universities",
  { code: "UAEH-ESTE" },
  { $set: { code: "UAEH-ESTE", name: "Universidad Autónoma del Estado de Hidalgo, Escuela Superior de Tepeji del Río", city: "Tepeji del Río de Ocampo", country: "México", searchKey: "uaeh este universidad autonoma del estado de hidalgo escuela superior de tepeji del rio tepeji del rio de ocampo", location: { type: "Point", coordinates: [-99.36116, 19.91703] } }, $setOnInsert: { createdAt: ISODate("2026-11-02T00:00:00.000Z") } });
upsert("universities",
  { code: "UAEH-ESTL" },
  { $set: { code: "UAEH-ESTL", name: "Universidad Autónoma del Estado de Hidalgo, Escuela Superior de Tlahuelilpan", city: "Tlahuelilpan", country: "México", searchKey: "uaeh estl universidad autonoma del estado de hidalgo escuela superior de tlahuelilpan tlahuelilpan", location: { type: "Point", coordinates: [-99.23041, 20.12991] } }, $setOnInsert: { createdAt: ISODate("2026-11-02T00:00:00.000Z") } });
upsert("universities",
  { code: "UAEH-ESAT" },
  { $set: { code: "UAEH-ESAT", name: "Universidad Autónoma del Estado de Hidalgo, Escuela Superior de Atotonilco de Tula", city: "Atotonilco de Tula", country: "México", searchKey: "uaeh esat universidad autonoma del estado de hidalgo escuela superior de atotonilco de tula atotonilco de tula", location: { type: "Point", coordinates: [-99.23601, 20.01584] } }, $setOnInsert: { createdAt: ISODate("2026-11-02T00:00:00.000Z") } });
upsert("universities",
  { code: "ITSOEH" },
  { $set: { code: "ITSOEH", name: "Instituto Tecnológico Superior del Occidente del Estado de Hidalgo", city: "Mixquiahuala de Juárez", country: "México", searchKey: "itsoeh instituto tecnologico superior del occidente del estado de hidalgo mixquiahuala de juarez", location: { type: "Point", coordinates: [-99.22178, 20.20551] } }, $setOnInsert: { createdAt: ISODate("2026-11-02T00:00:00.000Z") } });
upsert("universities",
  { code: "TESJI" },
  { $set: { code: "TESJI", name: "Tecnológico de Estudios Superiores de Jilotepec", city: "Jilotepec", country: "México", searchKey: "tesji tecnologico de estudios superiores de jilotepec jilotepec", location: { type: "Point", coordinates: [-99.57719, 19.91953] } }, $setOnInsert: { createdAt: ISODate("2026-11-02T00:00:00.000Z") } });
upsert("universities",
  { code: "UMB-JILO" },
  { $set: { code: "UMB-JILO", name: "Universidad Mexiquense del Bicentenario, Unidad de Estudios Superiores Jilotepec", city: "Jilotepec", country: "México", searchKey: "umb jilo universidad mexiquense del bicentenario unidad de estudios superiores jilotepec jilotepec", location: { type: "Point", coordinates: [-99.57849, 19.91248] } }, $setOnInsert: { createdAt: ISODate("2026-11-02T00:00:00.000Z") } });
upsert("universities",
  { code: "UMB-VDC" },
  { $set: { code: "UMB-VDC", name: "Universidad Mexiquense del Bicentenario, Unidad de Estudios Superiores Villa del Carbón", city: "Villa del Carbón", country: "México", searchKey: "umb vdc universidad mexiquense del bicentenario unidad de estudios superiores villa del carbon villa del carbon", location: { type: "Point", coordinates: [-99.46312, 19.72791] } }, $setOnInsert: { createdAt: ISODate("2026-11-02T00:00:00.000Z") } });
upsert("universities",
  { code: "UCAN" },
  { $set: { code: "UCAN", name: "Universidad Canadiense", city: "Tula de Allende", country: "México", searchKey: "ucan universidad canadiense tula de allende", location: { type: "Point", coordinates: [-99.34507, 20.0517] } }, $setOnInsert: { createdAt: ISODate("2026-11-02T00:00:00.000Z") } });
upsert("universities",
  { code: "UNE-TULA" },
  { $set: { code: "UNE-TULA", name: "Universidad del Nuevo México", city: "Tula de Allende", country: "México", searchKey: "une tula universidad del nuevo mexico tula de allende", location: { type: "Point", coordinates: [-99.33629, 20.03108] } }, $setOnInsert: { createdAt: ISODate("2026-11-02T00:00:00.000Z") } });

// properties (12)
upsert("properties",
  { _id: ObjectId("000000000000000000000001") },
  { $set: { title: "Cuarto amueblado cerca de la UTTT", description: "Cuarto individual amueblado a distancia caminable de la universidad, con internet incluido.", price: NumberDecimal("2200.0"), currency: "MXN", city: "Tula de Allende", universityId: universityId("UTTT"), bedrooms: 1, bathrooms: 1, areaM2: Double(16), amenities: ["wifi", "amueblado"] }, $setOnInsert: { createdAt: ISODate("2026-11-02T00:00:00.000Z") } });
upsert("properties",
  { _id: ObjectId("000000000000000000000002") },
  { $set: { title: "Departamento de 2 recámaras en El Carmen", description: "Departamento para compartir entre dos estudiantes, con cocina y estacionamiento.", price: NumberDecimal("6500.0"), currency: "MXN", city: "Tula de Allende", universityId: universityId("UTTT"), bedrooms: 2, bathrooms: 1, areaM2: Double(62), amenities: ["wifi", "estacionamiento", "cocina equipada"] }, $setOnInsert: { createdAt: ISODate("2026-11-02T00:00:00.000Z") } });
upsert("properties",
  { _id: ObjectId("000000000000000000000003") },
  { $set: { title: "Cuarto con baño propio en Tula centro", description: "Cuarto con baño privado y servicios incluidos, cerca del transporte público.", price: NumberDecimal("2800.0"), currency: "MXN", city: "Tula de Allende", universityId: universityId("UCAN"), bedrooms: 1, bathrooms: 1, areaM2: Double(20), amenities: ["wifi", "baño privado", "servicios incluidos"] }, $setOnInsert: { createdAt: ISODate("2026-11-02T00:00:00.000Z") } });
upsert("properties",
  { _id: ObjectId("000000000000000000000004") },
  { $set: { title: "Estudio para estudiante en Tula", description: "Estudio compacto con cocineta, ideal para una persona.", price: NumberDecimal("3500.0"), currency: "MXN", city: "Tula de Allende", universityId: universityId("UNE-TULA"), bedrooms: 1, bathrooms: 1, areaM2: Double(28), amenities: ["wifi", "amueblado", "cocina equipada"] }, $setOnInsert: { createdAt: ISODate("2026-11-02T00:00:00.000Z") } });
upsert("properties",
  { _id: ObjectId("000000000000000000000005") },
  { $set: { title: "Cuarto en casa compartida en Tepeji", description: "Cuarto en casa compartida con áreas comunes y lavadora.", price: NumberDecimal("1900.0"), currency: "MXN", city: "Tepeji del Río de Ocampo", universityId: universityId("UAEH-ESTE"), bedrooms: 1, bathrooms: 1, areaM2: Double(14), amenities: ["wifi", "lavadora"] }, $setOnInsert: { createdAt: ISODate("2026-11-02T00:00:00.000Z") } });
upsert("properties",
  { _id: ObjectId("000000000000000000000006") },
  { $set: { title: "Departamento pequeño en Tepeji centro", description: "Departamento de una recámara con cocina y servicios básicos.", price: NumberDecimal("5200.0"), currency: "MXN", city: "Tepeji del Río de Ocampo", universityId: universityId("UAEH-ESTE"), bedrooms: 1, bathrooms: 1, areaM2: Double(40), amenities: ["wifi", "cocina equipada"] }, $setOnInsert: { createdAt: ISODate("2026-11-02T00:00:00.000Z") } });
upsert("properties",
  { _id: ObjectId("000000000000000000000007") },
  { $set: { title: "Cuarto cerca de la Escuela Superior de Tlahuelilpan", description: "Cuarto sencillo con internet, a pocos minutos de la escuela.", price: NumberDecimal("1800.0"), currency: "MXN", city: "Tlahuelilpan", universityId: universityId("UAEH-ESTL"), bedrooms: 1, bathrooms: 1, areaM2: Double(14), amenities: ["wifi"] }, $setOnInsert: { createdAt: ISODate("2026-11-02T00:00:00.000Z") } });
upsert("properties",
  { _id: ObjectId("000000000000000000000008") },
  { $set: { title: "Cuarto amueblado en Atotonilco de Tula", description: "Cuarto amueblado con servicios incluidos y acceso a cocina compartida.", price: NumberDecimal("2000.0"), currency: "MXN", city: "Atotonilco de Tula", universityId: universityId("UAEH-ESAT"), bedrooms: 1, bathrooms: 1, areaM2: Double(15), amenities: ["wifi", "amueblado", "servicios incluidos"] }, $setOnInsert: { createdAt: ISODate("2026-11-02T00:00:00.000Z") } });
upsert("properties",
  { _id: ObjectId("000000000000000000000009") },
  { $set: { title: "Casa compartida en Mixquiahuala", description: "Cuarto en casa compartida con patio y estacionamiento.", price: NumberDecimal("2300.0"), currency: "MXN", city: "Mixquiahuala de Juárez", universityId: universityId("ITSOEH"), bedrooms: 1, bathrooms: 1, areaM2: Double(16), amenities: ["wifi", "estacionamiento"] }, $setOnInsert: { createdAt: ISODate("2026-11-02T00:00:00.000Z") } });
upsert("properties",
  { _id: ObjectId("000000000000000000000010") },
  { $set: { title: "Cuarto cerca del TESJI", description: "Cuarto individual con internet y escritorio para estudiar.", price: NumberDecimal("2100.0"), currency: "MXN", city: "Jilotepec", universityId: universityId("TESJI"), bedrooms: 1, bathrooms: 1, areaM2: Double(15), amenities: ["wifi", "amueblado"] }, $setOnInsert: { createdAt: ISODate("2026-11-02T00:00:00.000Z") } });
upsert("properties",
  { _id: ObjectId("000000000000000000000011") },
  { $set: { title: "Departamento de 1 recámara en Jilotepec", description: "Departamento independiente con cocina, cerca de la unidad de la UMB.", price: NumberDecimal("4800.0"), currency: "MXN", city: "Jilotepec", universityId: universityId("UMB-JILO"), bedrooms: 1, bathrooms: 1, areaM2: Double(38), amenities: ["wifi", "cocina equipada", "estacionamiento"] }, $setOnInsert: { createdAt: ISODate("2026-11-02T00:00:00.000Z") } });
upsert("properties",
  { _id: ObjectId("000000000000000000000012") },
  { $set: { title: "Cuarto con servicios incluidos en Villa del Carbón", description: "Cuarto con agua, luz e internet incluidos en la renta.", price: NumberDecimal("1900.0"), currency: "MXN", city: "Villa del Carbón", universityId: universityId("UMB-VDC"), bedrooms: 1, bathrooms: 1, areaM2: Double(14), amenities: ["wifi", "servicios incluidos"] }, $setOnInsert: { createdAt: ISODate("2026-11-02T00:00:00.000Z") } });

// places (60)
upsert("places",
  { osmNodeId: NumberLong("459391865") },
  { $set: { name: "Ajacuba", nameNormalized: "ajacuba", type: "city", city: "Ajacuba", location: { type: "Point", coordinates: [-99.12168, 20.09111] } }, $setOnInsert: { osmNodeId: NumberLong("459391865") } });
upsert("places",
  { osmNodeId: NumberLong("6887572767") },
  { $set: { name: "Apaxco de Ocampo", nameNormalized: "apaxco de ocampo", type: "city", city: "Apaxco de Ocampo", location: { type: "Point", coordinates: [-99.17161, 19.97564] } }, $setOnInsert: { osmNodeId: NumberLong("6887572767") } });
upsert("places",
  { osmNodeId: NumberLong("459391894") },
  { $set: { name: "Atitalaquia", nameNormalized: "atitalaquia", type: "city", city: "Atitalaquia", location: { type: "Point", coordinates: [-99.22149, 20.05928] } }, $setOnInsert: { osmNodeId: NumberLong("459391894") } });
upsert("places",
  { osmNodeId: NumberLong("6887572768") },
  { $set: { name: "Atotonilco de Tula", nameNormalized: "atotonilco de tula", type: "city", city: "Atotonilco de Tula", location: { type: "Point", coordinates: [-99.21958, 20.00806] } }, $setOnInsert: { osmNodeId: NumberLong("6887572768") } });
upsert("places",
  { osmNodeId: NumberLong("459392274") },
  { $set: { name: "Bomintzha", nameNormalized: "bomintzha", type: "city", city: "Bomintzha", location: { type: "Point", coordinates: [-99.26944, 20.01083] } }, $setOnInsert: { osmNodeId: NumberLong("459392274") } });
upsert("places",
  { osmNodeId: NumberLong("459392257") },
  { $set: { name: "Colonia Cerro de la Cruz", nameNormalized: "colonia cerro de la cruz", type: "city", city: "Colonia Cerro de la Cruz", location: { type: "Point", coordinates: [-99.22306, 20.14306] } }, $setOnInsert: { osmNodeId: NumberLong("459392257") } });
upsert("places",
  { osmNodeId: NumberLong("459392256") },
  { $set: { name: "Colonia Cuauhtémoc", nameNormalized: "colonia cuauhtemoc", type: "city", city: "Colonia Cuauhtémoc", location: { type: "Point", coordinates: [-99.23944, 20.13917] } }, $setOnInsert: { osmNodeId: NumberLong("459392256") } });
upsert("places",
  { osmNodeId: NumberLong("459392015") },
  { $set: { name: "Colonia Palmillas", nameNormalized: "colonia palmillas", type: "city", city: "Colonia Palmillas", location: { type: "Point", coordinates: [-99.19833, 20.21778] } }, $setOnInsert: { osmNodeId: NumberLong("459392015") } });
upsert("places",
  { osmNodeId: NumberLong("459392231") },
  { $set: { name: "Doxey", nameNormalized: "doxey", type: "city", city: "Doxey", location: { type: "Point", coordinates: [-99.24253, 20.09102] } }, $setOnInsert: { osmNodeId: NumberLong("459392231") } });
upsert("places",
  { osmNodeId: NumberLong("459392235") },
  { $set: { name: "Ignacio Zaragoza", nameNormalized: "ignacio zaragoza", type: "city", city: "Ignacio Zaragoza", location: { type: "Point", coordinates: [-99.32217, 20.01635] } }, $setOnInsert: { osmNodeId: NumberLong("459392235") } });
upsert("places",
  { osmNodeId: NumberLong("302001640") },
  { $set: { name: "Jilotepec de Molina Enriquez", nameNormalized: "jilotepec de molina enriquez", type: "city", city: "Jilotepec de Molina Enriquez", location: { type: "Point", coordinates: [-99.53747, 19.95324] } }, $setOnInsert: { osmNodeId: NumberLong("302001640") } });
upsert("places",
  { osmNodeId: NumberLong("459392282") },
  { $set: { name: "La Amistad", nameNormalized: "la amistad", type: "city", city: "La Amistad", location: { type: "Point", coordinates: [-99.31806, 20.00694] } }, $setOnInsert: { osmNodeId: NumberLong("459392282") } });
upsert("places",
  { osmNodeId: NumberLong("459391913") },
  { $set: { name: "La Cañada", nameNormalized: "la canada", type: "city", city: "La Cañada", location: { type: "Point", coordinates: [-99.24306, 19.99611] } }, $setOnInsert: { osmNodeId: NumberLong("459391913") } });
upsert("places",
  { osmNodeId: NumberLong("459392242") },
  { $set: { name: "La Cruz", nameNormalized: "la cruz", type: "city", city: "La Cruz", location: { type: "Point", coordinates: [-99.24194, 20.17583] } }, $setOnInsert: { osmNodeId: NumberLong("459392242") } });
upsert("places",
  { osmNodeId: NumberLong("459392210") },
  { $set: { name: "Mangas", nameNormalized: "mangas", type: "city", city: "Mangas", location: { type: "Point", coordinates: [-99.24806, 20.1875] } }, $setOnInsert: { osmNodeId: NumberLong("459392210") } });
upsert("places",
  { osmNodeId: NumberLong("297551891") },
  { $set: { name: "Mixquiahuala", nameNormalized: "mixquiahuala", type: "city", city: "Mixquiahuala", location: { type: "Point", coordinates: [-99.2098, 20.23073] } }, $setOnInsert: { osmNodeId: NumberLong("297551891") } });
upsert("places",
  { osmNodeId: NumberLong("459392275") },
  { $set: { name: "Monte Alegre", nameNormalized: "monte alegre", type: "city", city: "Monte Alegre", location: { type: "Point", coordinates: [-99.34056, 19.98167] } }, $setOnInsert: { osmNodeId: NumberLong("459392275") } });
upsert("places",
  { osmNodeId: NumberLong("459392251") },
  { $set: { name: "Munitepec de Madero", nameNormalized: "munitepec de madero", type: "city", city: "Munitepec de Madero", location: { type: "Point", coordinates: [-99.19874, 20.12901] } }, $setOnInsert: { osmNodeId: NumberLong("459392251") } });
upsert("places",
  { osmNodeId: NumberLong("459392276") },
  { $set: { name: "Nantzha", nameNormalized: "nantzha", type: "city", city: "Nantzha", location: { type: "Point", coordinates: [-99.37028, 20.05333] } }, $setOnInsert: { osmNodeId: NumberLong("459392276") } });
upsert("places",
  { osmNodeId: NumberLong("459392806") },
  { $set: { name: "Ocampo", nameNormalized: "ocampo", type: "city", city: "Ocampo", location: { type: "Point", coordinates: [-99.23834, 20.00846] } }, $setOnInsert: { osmNodeId: NumberLong("459392806") } });
upsert("places",
  { osmNodeId: NumberLong("459392208") },
  { $set: { name: "Presas", nameNormalized: "presas", type: "city", city: "Presas", location: { type: "Point", coordinates: [-99.25278, 20.15889] } }, $setOnInsert: { osmNodeId: NumberLong("459392208") } });
upsert("places",
  { osmNodeId: NumberLong("459391780") },
  { $set: { name: "Progreso", nameNormalized: "progreso", type: "city", city: "Progreso", location: { type: "Point", coordinates: [-99.18907, 20.24779] } }, $setOnInsert: { osmNodeId: NumberLong("459391780") } });
upsert("places",
  { osmNodeId: NumberLong("459391911") },
  { $set: { name: "Progreso 1a Sección", nameNormalized: "progreso 1a seccion", type: "city", city: "Progreso 1a Sección", location: { type: "Point", coordinates: [-99.23707, 20.01928] } }, $setOnInsert: { osmNodeId: NumberLong("459391911") } });
upsert("places",
  { osmNodeId: NumberLong("3791593456") },
  { $set: { name: "Pueblo Nuevo de Jasso", nameNormalized: "pueblo nuevo de jasso", type: "city", city: "Pueblo Nuevo de Jasso", location: { type: "Point", coordinates: [-99.32177, 19.98482] } }, $setOnInsert: { osmNodeId: NumberLong("3791593456") } });
upsert("places",
  { osmNodeId: NumberLong("459392277") },
  { $set: { name: "San Andrés Tultepec", nameNormalized: "san andres tultepec", type: "city", city: "San Andrés Tultepec", location: { type: "Point", coordinates: [-99.37833, 20.05417] } }, $setOnInsert: { osmNodeId: NumberLong("459392277") } });
upsert("places",
  { osmNodeId: NumberLong("10046714529") },
  { $set: { name: "San Francisco Soyaniquilpan", nameNormalized: "san francisco soyaniquilpan", type: "city", city: "San Francisco Soyaniquilpan", location: { type: "Point", coordinates: [-99.52872, 20.0153] } }, $setOnInsert: { osmNodeId: NumberLong("10046714529") } });
upsert("places",
  { osmNodeId: NumberLong("459392191") },
  { $set: { name: "San Ildefonso", nameNormalized: "san ildefonso", type: "city", city: "San Ildefonso", location: { type: "Point", coordinates: [-99.36917, 19.99444] } }, $setOnInsert: { osmNodeId: NumberLong("459392191") } });
upsert("places",
  { osmNodeId: NumberLong("459395480") },
  { $set: { name: "San Isidro Presas", nameNormalized: "san isidro presas", type: "city", city: "San Isidro Presas", location: { type: "Point", coordinates: [-99.26039, 20.15151] } }, $setOnInsert: { osmNodeId: NumberLong("459395480") } });
upsert("places",
  { osmNodeId: NumberLong("2381858815") },
  { $set: { name: "San Jerónimo Zacapexco", nameNormalized: "san jeronimo zacapexco", type: "city", city: "San Jerónimo Zacapexco", location: { type: "Point", coordinates: [-99.477, 19.72233] } }, $setOnInsert: { osmNodeId: NumberLong("2381858815") } });
upsert("places",
  { osmNodeId: NumberLong("459391799") },
  { $set: { name: "San Marcos", nameNormalized: "san marcos", type: "city", city: "San Marcos", location: { type: "Point", coordinates: [-99.33663, 20.02816] } }, $setOnInsert: { osmNodeId: NumberLong("459391799") } });
upsert("places",
  { osmNodeId: NumberLong("459392190") },
  { $set: { name: "Santa Ana Atzcapotzaltongo", nameNormalized: "santa ana atzcapotzaltongo", type: "city", city: "Santa Ana Atzcapotzaltongo", location: { type: "Point", coordinates: [-99.385, 19.94417] } }, $setOnInsert: { osmNodeId: NumberLong("459392190") } });
upsert("places",
  { osmNodeId: NumberLong("459392212") },
  { $set: { name: "Santa María Batha", nameNormalized: "santa maria batha", type: "city", city: "Santa María Batha", location: { type: "Point", coordinates: [-99.24944, 20.22722] } }, $setOnInsert: { osmNodeId: NumberLong("459392212") } });
upsert("places",
  { osmNodeId: NumberLong("459392230") },
  { $set: { name: "Teltipán de Juárez", nameNormalized: "teltipan de juarez", type: "city", city: "Teltipán de Juárez", location: { type: "Point", coordinates: [-99.20167, 20.11444] } }, $setOnInsert: { osmNodeId: NumberLong("459392230") } });
upsert("places",
  { osmNodeId: NumberLong("459391795") },
  { $set: { name: "Tepeji del Río", nameNormalized: "tepeji del rio", type: "city", city: "Tepeji del Río", location: { type: "Point", coordinates: [-99.34306, 19.90851] } }, $setOnInsert: { osmNodeId: NumberLong("459391795") } });
upsert("places",
  { osmNodeId: NumberLong("459392220") },
  { $set: { name: "Tetepango", nameNormalized: "tetepango", type: "city", city: "Tetepango", location: { type: "Point", coordinates: [-99.15771, 20.10257] } }, $setOnInsert: { osmNodeId: NumberLong("459392220") } });
upsert("places",
  { osmNodeId: NumberLong("459392168") },
  { $set: { name: "Tinajas", nameNormalized: "tinajas", type: "city", city: "Tinajas", location: { type: "Point", coordinates: [-99.36694, 19.89806] } }, $setOnInsert: { osmNodeId: NumberLong("459392168") } });
upsert("places",
  { osmNodeId: NumberLong("459392250") },
  { $set: { name: "Tlahuelilpan", nameNormalized: "tlahuelilpan", type: "city", city: "Tlahuelilpan", location: { type: "Point", coordinates: [-99.23041, 20.12991] } }, $setOnInsert: { osmNodeId: NumberLong("459392250") } });
upsert("places",
  { osmNodeId: NumberLong("459391898") },
  { $set: { name: "Tlamaco", nameNormalized: "tlamaco", type: "city", city: "Tlamaco", location: { type: "Point", coordinates: [-99.23034, 20.03376] } }, $setOnInsert: { osmNodeId: NumberLong("459391898") } });
upsert("places",
  { osmNodeId: NumberLong("459391797") },
  { $set: { name: "Tlaxcoapan", nameNormalized: "tlaxcoapan", type: "city", city: "Tlaxcoapan", location: { type: "Point", coordinates: [-99.22197, 20.08976] } }, $setOnInsert: { osmNodeId: NumberLong("459391797") } });
upsert("places",
  { osmNodeId: NumberLong("10299764131") },
  { $set: { name: "16 de enero", nameNormalized: "16 de enero", type: "neighborhood", city: "Tula", location: { type: "Point", coordinates: [-99.34499, 20.06502] } }, $setOnInsert: { osmNodeId: NumberLong("10299764131") } });
upsert("places",
  { osmNodeId: NumberLong("10299764128") },
  { $set: { name: "Alvarado", nameNormalized: "alvarado", type: "neighborhood", city: "Tula", location: { type: "Point", coordinates: [-99.35607, 20.04702] } }, $setOnInsert: { osmNodeId: NumberLong("10299764128") } });
upsert("places",
  { osmNodeId: NumberLong("10299764122") },
  { $set: { name: "Barrio Alto", nameNormalized: "barrio alto", type: "neighborhood", city: "Tula", location: { type: "Point", coordinates: [-99.34673, 20.05103] } }, $setOnInsert: { osmNodeId: NumberLong("10299764122") } });
upsert("places",
  { osmNodeId: NumberLong("10299764121") },
  { $set: { name: "Centro", nameNormalized: "centro", type: "neighborhood", city: "Tula", location: { type: "Point", coordinates: [-99.34306, 20.05668] } }, $setOnInsert: { osmNodeId: NumberLong("10299764121") } });
upsert("places",
  { osmNodeId: NumberLong("10299764126") },
  { $set: { name: "Chapultepec", nameNormalized: "chapultepec", type: "neighborhood", city: "Tula", location: { type: "Point", coordinates: [-99.33887, 20.0473] } }, $setOnInsert: { osmNodeId: NumberLong("10299764126") } });
upsert("places",
  { osmNodeId: NumberLong("459391802") },
  { $set: { name: "Ciudad Cooperativa Cruz Azul", nameNormalized: "ciudad cooperativa cruz azul", type: "neighborhood", city: "Tula", location: { type: "Point", coordinates: [-99.3266, 19.98967] } }, $setOnInsert: { osmNodeId: NumberLong("459391802") } });
upsert("places",
  { osmNodeId: NumberLong("10299764133") },
  { $set: { name: "El Cielito", nameNormalized: "el cielito", type: "neighborhood", city: "Tula", location: { type: "Point", coordinates: [-99.3301, 20.05421] } }, $setOnInsert: { osmNodeId: NumberLong("10299764133") } });
upsert("places",
  { osmNodeId: NumberLong("459391800") },
  { $set: { name: "El Llano", nameNormalized: "el llano", type: "neighborhood", city: "Tula", location: { type: "Point", coordinates: [-99.31881, 20.06548] } }, $setOnInsert: { osmNodeId: NumberLong("459391800") } });
upsert("places",
  { osmNodeId: NumberLong("10299764127") },
  { $set: { name: "FOVISSSTE", nameNormalized: "fovissste", type: "neighborhood", city: "Tula", location: { type: "Point", coordinates: [-99.3517, 20.05054] } }, $setOnInsert: { osmNodeId: NumberLong("10299764127") } });
upsert("places",
  { osmNodeId: NumberLong("10299764134") },
  { $set: { name: "Jalpa", nameNormalized: "jalpa", type: "neighborhood", city: "Tula", location: { type: "Point", coordinates: [-99.3414, 20.04474] } }, $setOnInsert: { osmNodeId: NumberLong("10299764134") } });
upsert("places",
  { osmNodeId: NumberLong("10299764130") },
  { $set: { name: "La Malinche", nameNormalized: "la malinche", type: "neighborhood", city: "Tula", location: { type: "Point", coordinates: [-99.34781, 20.05801] } }, $setOnInsert: { osmNodeId: NumberLong("10299764130") } });
upsert("places",
  { osmNodeId: NumberLong("10299764129") },
  { $set: { name: "La Malinche 2da. sección", nameNormalized: "la malinche 2da seccion", type: "neighborhood", city: "Tula", location: { type: "Point", coordinates: [-99.35327, 20.05859] } }, $setOnInsert: { osmNodeId: NumberLong("10299764129") } });
upsert("places",
  { osmNodeId: NumberLong("10299764123") },
  { $set: { name: "Loma Blanca", nameNormalized: "loma blanca", type: "neighborhood", city: "Tula", location: { type: "Point", coordinates: [-99.34495, 20.0447] } }, $setOnInsert: { osmNodeId: NumberLong("10299764123") } });
upsert("places",
  { osmNodeId: NumberLong("10299764124") },
  { $set: { name: "Los Fresnos", nameNormalized: "los fresnos", type: "neighborhood", city: "Tula", location: { type: "Point", coordinates: [-99.34179, 20.05154] } }, $setOnInsert: { osmNodeId: NumberLong("10299764124") } });
upsert("places",
  { osmNodeId: NumberLong("10299764125") },
  { $set: { name: "Los Sabinos", nameNormalized: "los sabinos", type: "neighborhood", city: "Tula", location: { type: "Point", coordinates: [-99.33063, 20.04746] } }, $setOnInsert: { osmNodeId: NumberLong("10299764125") } });
upsert("places",
  { osmNodeId: NumberLong("8591337291") },
  { $set: { name: "Sán José", nameNormalized: "san jose", type: "neighborhood", city: "Tula", location: { type: "Point", coordinates: [-99.35079, 20.04382] } }, $setOnInsert: { osmNodeId: NumberLong("8591337291") } });
upsert("places",
  { osmNodeId: NumberLong("459391798") },
  { $set: { name: "Tula", nameNormalized: "tula", type: "city", city: "Tula", location: { type: "Point", coordinates: [-99.34507, 20.0517] } }, $setOnInsert: { osmNodeId: NumberLong("459391798") } });
upsert("places",
  { osmNodeId: NumberLong("10299764132") },
  { $set: { name: "U.H. PEMEX", nameNormalized: "u h pemex", type: "neighborhood", city: "Tula", location: { type: "Point", coordinates: [-99.33748, 20.05897] } }, $setOnInsert: { osmNodeId: NumberLong("10299764132") } });
upsert("places",
  { osmNodeId: NumberLong("459391899") },
  { $set: { name: "Unidad Habitacional Antonio Osorio de León (Bojay)", nameNormalized: "unidad habitacional antonio osorio de leon bojay", type: "city", city: "Unidad Habitacional Antonio Osorio de León (Bojay)", location: { type: "Point", coordinates: [-99.24472, 20.04333] } }, $setOnInsert: { osmNodeId: NumberLong("459391899") } });
upsert("places",
  { osmNodeId: NumberLong("10900967466") },
  { $set: { name: "Villa del Carbón", nameNormalized: "villa del carbon", type: "city", city: "Villa del Carbón", location: { type: "Point", coordinates: [-99.46312, 19.72791] } }, $setOnInsert: { osmNodeId: NumberLong("10900967466") } });
upsert("places",
  { osmNodeId: NumberLong("459392809") },
  { $set: { name: "Zacamulpa", nameNormalized: "zacamulpa", type: "city", city: "Zacamulpa", location: { type: "Point", coordinates: [-99.25819, 20.00603] } }, $setOnInsert: { osmNodeId: NumberLong("459392809") } });

for (const [collection, entry] of Object.entries(totals)) {
  const total = target.getCollection(collection).countDocuments({});
  print(`${collection}: ${entry.inserted} insertados, ${entry.updated} actualizados, ${entry.unchanged} sin cambios (${total} en total)`);
}
print(`Catálogos listos en ${dbName}.`);
