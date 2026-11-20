// NV-26 — Esquema de la base de datos de TheHouse: colecciones, índices y buckets de GridFS.
//
// ARCHIVO GENERADO desde el código por TheHouse.IntegrationTests/DatabaseScriptTests.cs:
// no se edita a mano (ver scripts/db/README.md para regenerarlo).
//
// Uso:   mongosh "<cadena de conexión>" --file scripts/db/01-schema.mongodb.js
// Base:  variable de entorno THEHOUSE_DB (por defecto thehouse_u2).
// Idempotente: solo crea o actualiza lo que falta; nunca borra colecciones, índices ni documentos.
// No contiene cadenas de conexión, usuarios, contraseñas ni datos personales.

const dbName = (typeof process !== "undefined" && process.env.THEHOUSE_DB) || "thehouse_u2";
const target = db.getSiblingDB(dbName);

const collections = [
  "agreements",
  "audit_events",
  "buildings",
  "conversations",
  "deleted_accounts",
  "favorites",
  "landlord_profiles",
  "media_items",
  "messages",
  "moderation_rejections",
  "notifications",
  "places",
  "properties",
  "push_devices",
  "refresh_tokens",
  "rental_requests",
  "reports",
  "reviews",
  "room_views",
  "roommate_profiles",
  "rooms",
  "staff_roles",
  "universities",
  "user_preferences",
  "user_profiles",
  "user_suspensions",
  "users",
  "verification_documents",
  "verification_reviews",
  "visit_idempotency",
  "visits",
];

// [colección, llaves, opciones]: mismos nombres y opciones que los repositorios del código.
const indexes = [
  ["agreements", { roomId: 1 }, { name: "ux_agreements_open_room", unique: true, partialFilterExpression: { open: true } }],
  ["agreements", { "code.codeHash": 1 }, { name: "ix_agreements_code_hash", sparse: true }],
  ["agreements", { "code.qrTokenHash": 1 }, { name: "ix_agreements_qr_token_hash", sparse: true }],
  ["agreements", { ownerId: 1, createdAt: -1 }, { name: "ix_agreements_owner_created" }],
  ["agreements", { studentId: 1, status: 1 }, { name: "ix_agreements_student_status" }],
  ["buildings", { ownerId: 1, createdAt: -1 }, { name: "ix_buildings_owner_created" }],
  ["buildings", { location: "2dsphere" }, { name: "ix_buildings_location_2dsphere" }],
  ["conversations", { requestId: 1 }, { name: "ux_conversations_request", unique: true }],
  ["conversations", { studentId: 1, lastMessageAt: -1 }, { name: "ix_conversations_student_last" }],
  ["conversations", { ownerId: 1, lastMessageAt: -1 }, { name: "ix_conversations_owner_last" }],
  ["favorites", { userId: 1, roomId: 1 }, { name: "ux_favorites_user_room", unique: true, partialFilterExpression: { roomId: { $type: 2 } } }],
  ["favorites", { userId: 1, createdAt: -1 }, { name: "ix_favorites_user_created_v2" }],
  ["landlord_profiles", { userId: 1 }, { name: "ux_landlord_profiles_user", unique: true }],
  ["media_items", { buildingId: 1, roomId: 1 }, { name: "ix_media_items_building_room" }],
  ["media_items", { ownerId: 1 }, { name: "ix_media_items_owner" }],
  ["messages", { conversationId: 1, _id: 1 }, { name: "ix_messages_conversation_id" }],
  ["messages", { conversationId: 1, senderId: 1, clientMessageId: 1 }, { name: "ux_messages_client_id", unique: true }],
  ["moderation_rejections", { userId: 1, createdAt: -1 }, { name: "ix_moderation_rejections_user_created" }],
  ["moderation_rejections", { createdAt: 1 }, { name: "ttl_moderation_rejections_created", expireAfterSeconds: 15552000 }],
  ["notifications", { userId: 1, dedupeKey: 1 }, { name: "ux_notifications_unread_user_key", unique: true, partialFilterExpression: { unread: true } }],
  ["notifications", { userId: 1, updatedAt: -1 }, { name: "ix_notifications_user_updated" }],
  ["notifications", { userId: 1, unread: 1, category: 1 }, { name: "ix_notifications_user_unread_category" }],
  ["notifications", { createdAt: 1 }, { name: "ttl_notifications_created", expireAfterSeconds: 7776000 }],
  ["places", { osmNodeId: 1 }, { name: "ux_places_osm", unique: true }],
  ["places", { location: "2dsphere" }, { name: "ix_places_location_2dsphere" }],
  ["properties", { createdAt: -1, _id: -1 }, { name: "ix_properties_createdAt_id_desc" }],
  ["push_devices", { userId: 1, installationId: 1 }, { name: "ux_push_devices_user_installation", unique: true }],
  ["push_devices", { fcmToken: 1 }, { name: "ux_push_devices_token", unique: true }],
  ["push_devices", { updatedAt: 1 }, { name: "ttl_push_devices_updated", expireAfterSeconds: 23328000 }],
  ["refresh_tokens", { tokenHash: 1 }, { name: "ux_refresh_tokens_hash", unique: true }],
  ["refresh_tokens", { sessionId: 1 }, { name: "ix_refresh_tokens_session" }],
  ["refresh_tokens", { expiresAt: 1 }, { name: "ttl_refresh_tokens_expires", expireAfterSeconds: 0 }],
  ["rental_requests", { studentId: 1, roomId: 1 }, { name: "ux_rental_requests_active_student_room", unique: true, partialFilterExpression: { active: true } }],
  ["rental_requests", { studentId: 1, updatedAt: -1 }, { name: "ix_rental_requests_student_updated" }],
  ["rental_requests", { ownerId: 1, status: 1, updatedAt: -1 }, { name: "ix_rental_requests_owner_status_updated" }],
  ["reports", { reporterId: 1, targetType: 1, targetId: 1 }, { name: "ux_reports_open_reporter_target", unique: true, partialFilterExpression: { status: "open" } }],
  ["reports", { status: 1, createdAt: 1 }, { name: "ix_reports_status_created" }],
  ["reviews", { authorId: 1, roomId: 1 }, { name: "ux_reviews_author_room", unique: true }],
  ["reviews", { roomId: 1, status: 1, createdAt: -1 }, { name: "ix_reviews_room_status_created" }],
  ["reviews", { ownerId: 1, status: 1 }, { name: "ix_reviews_owner_status" }],
  ["reviews", { authorId: 1, createdAt: -1 }, { name: "ix_reviews_author_created" }],
  ["room_views", { roomId: 1, buildingId: 1, day: 1, actorHash: 1 }, { name: "ux_room_views_room_day_actor", unique: true }],
  ["room_views", { ownerId: 1, day: 1 }, { name: "ix_room_views_owner_day" }],
  ["room_views", { createdAt: 1 }, { name: "ttl_room_views_created", expireAfterSeconds: 34560000 }],
  ["roommate_profiles", { userId: 1 }, { name: "ux_roommate_profiles_user", unique: true }],
  ["roommate_profiles", { universityId: 1, visible: 1, expiresAt: 1 }, { name: "ix_roommate_profiles_university_visible_expires" }],
  ["rooms", { buildingId: 1, number: 1 }, { name: "ux_rooms_building_number", unique: true }],
  ["rooms", { ownerId: 1, status: 1 }, { name: "ix_rooms_owner_status" }],
  ["rooms", { status: 1, price: 1 }, { name: "ix_rooms_status_price" }],
  ["staff_roles", { userId: 1 }, { name: "ux_staff_roles_user", unique: true }],
  ["universities", { code: 1 }, { name: "ux_universities_code", unique: true }],
  ["universities", { name: 1, _id: 1 }, { name: "ix_universities_name_id_asc" }],
  ["universities", { location: "2dsphere" }, { name: "ix_universities_location_2dsphere" }],
  ["user_preferences", { userId: 1 }, { name: "ux_user_preferences_user", unique: true }],
  ["user_profiles", { userId: 1 }, { name: "ux_user_profiles_user", unique: true }],
  ["user_suspensions", { userId: 1 }, { name: "ux_user_suspensions_user", unique: true }],
  ["users", { email: 1 }, { name: "ux_users_email", unique: true }],
  ["verification_documents", { buildingId: 1, kind: 1 }, { name: "ux_verification_documents_building_kind", unique: true }],
  ["verification_documents", { ownerId: 1 }, { name: "ix_verification_documents_owner" }],
  ["verification_documents", { retentionUntil: 1 }, { name: "ix_verification_documents_retention", sparse: true }],
  ["visit_idempotency", { userId: 1, key: 1 }, { name: "ux_visit_idempotency_user_key", unique: true }],
  ["visit_idempotency", { createdAt: 1 }, { name: "ttl_visit_idempotency", expireAfterSeconds: 86400 }],
  ["visits", { userId: 1, scheduledAt: 1, _id: 1 }, { name: "ix_visits_user_scheduled" }],
  ["visits", { userId: 1, status: 1, scheduledAt: 1 }, { name: "ix_visits_user_status" }],
  ["visits", { roomId: 1, scheduledAt: 1 }, { name: "ix_visits_room_scheduled" }],
];

// Buckets de GridFS (`<bucket>.files` y `<bucket>.chunks`) con los índices de la especificación de GridFS.
const buckets = ["documents", "media"];
const gridFsIndexes = [
  ["files", { filename: 1, uploadDate: 1 }, { name: "filename_1_uploadDate_1" }],
  ["chunks", { files_id: 1, n: 1 }, { name: "files_id_1_n_1", unique: true }],
];

function ensureCollection(name) {
  if (target.getCollectionInfos({ name: name }, { nameOnly: true }).length > 0) {
    print(`colección existente: ${name}`);
    return;
  }
  target.createCollection(name);
  print(`colección creada:    ${name}`);
}

// Si ya existe un índice con el mismo nombre y otras opciones, MongoDB
// responde un error y el script se detiene: no borra ni recrea índices.
function ensureIndex(collection, keys, options) {
  const existed = target.getCollection(collection).getIndexes().some((index) => index.name === options.name);
  try {
    target.getCollection(collection).createIndex(keys, options);
  } catch (error) {
    print(`ERROR en el índice ${collection}.${options.name}: ${error.codeName || error.message}`);
    throw error;
  }
  print(`${existed ? "índice existente:" : "índice creado:   "} ${collection}.${options.name}`);
}

for (const name of collections) {
  ensureCollection(name);
}
for (const bucket of buckets) {
  for (const [suffix, keys, options] of gridFsIndexes) {
    ensureCollection(`${bucket}.${suffix}`);
    ensureIndex(`${bucket}.${suffix}`, keys, options);
  }
}
for (const [collection, keys, options] of indexes) {
  ensureIndex(collection, keys, options);
}

print(`Esquema listo en ${dbName}: ${collections.length} colecciones, ${indexes.length} índices y ${buckets.length} buckets de GridFS.`);
