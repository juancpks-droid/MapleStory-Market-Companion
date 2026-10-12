CREATE TABLE `reference_cache` (
	`key` text PRIMARY KEY NOT NULL,
	`body` text NOT NULL,
	`expires` integer NOT NULL
);
