import {sqliteTable,text,integer} from 'drizzle-orm/sqlite-core';
export const referenceCache=sqliteTable('reference_cache',{key:text('key').primaryKey(),body:text('body').notNull(),expires:integer('expires').notNull()});
