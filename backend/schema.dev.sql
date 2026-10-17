-- =====================================================================
-- Industrial Inventory Management System - Empty Local Development Schema
-- Creates the database and tables only; no application data is seeded.
-- =====================================================================

CREATE DATABASE IF NOT EXISTS `industrial_inventory`
  CHARACTER SET utf8mb4
  COLLATE utf8mb4_unicode_ci;

USE `industrial_inventory`;

-- ---------------------------------------------------------------------
-- Table: store_locations
-- ---------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `store_locations` (
  `id` VARCHAR(36) NOT NULL,
  `location_code` VARCHAR(50) NOT NULL,
  `location_name` VARCHAR(150) NOT NULL,
  `is_active` TINYINT(1) NOT NULL DEFAULT 1,
  `created_at` DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  `updated_at` DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6),
  PRIMARY KEY (`id`),
  UNIQUE KEY `uq_store_locations_code` (`location_code`),
  INDEX `ix_store_locations_is_active` (`is_active`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- ---------------------------------------------------------------------
-- Table: personnel
-- ---------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `personnel` (
  `id` VARCHAR(36) NOT NULL,
  `personnel_code` VARCHAR(50) NOT NULL,
  `name` VARCHAR(150) NOT NULL,
  `is_active` TINYINT(1) NOT NULL DEFAULT 1,
  `created_at` DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  `updated_at` DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6),
  PRIMARY KEY (`id`),
  UNIQUE KEY `uq_personnel_code` (`personnel_code`),
  INDEX `ix_personnel_is_active` (`is_active`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- ---------------------------------------------------------------------
-- Table: inventory_items
-- ---------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `inventory_items` (
  `id` VARCHAR(36) NOT NULL,
  `serial_number` VARCHAR(100) NOT NULL,
  `tag_number` VARCHAR(100) NOT NULL,
  `location_id` VARCHAR(36) NOT NULL,
  `registered_by_personnel_id` VARCHAR(36) NOT NULL,
  `is_issued` TINYINT(1) NOT NULL DEFAULT 0,
  `created_at` DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  `updated_at` DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6),
  PRIMARY KEY (`id`),
  UNIQUE KEY `uq_inventory_items_serial` (`serial_number`),
  UNIQUE KEY `uq_inventory_items_tag` (`tag_number`),
  INDEX `ix_inventory_items_location` (`location_id`),
  INDEX `ix_inventory_items_personnel` (`registered_by_personnel_id`),
  INDEX `ix_inventory_items_created_at` (`created_at` DESC),
  CONSTRAINT `fk_inventory_items_location`
    FOREIGN KEY (`location_id`) REFERENCES `store_locations` (`id`)
    ON DELETE RESTRICT ON UPDATE CASCADE,
  CONSTRAINT `fk_inventory_items_personnel`
    FOREIGN KEY (`registered_by_personnel_id`) REFERENCES `personnel` (`id`)
    ON DELETE RESTRICT ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- ---------------------------------------------------------------------
-- Table: inventory_movements
-- ---------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `inventory_movements` (
  `id` VARCHAR(36) NOT NULL,
  `inventory_item_id` VARCHAR(36) NOT NULL,
  `from_location_id` VARCHAR(36) NULL,
  `to_location_id` VARCHAR(36) NULL,
  `performed_by_personnel_id` VARCHAR(36) NOT NULL,
  `event_type` VARCHAR(50) NOT NULL DEFAULT 'INITIAL_REGISTRATION',
  `moved_at` DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  PRIMARY KEY (`id`),
  INDEX `ix_inventory_movements_item` (`inventory_item_id`),
  INDEX `ix_inventory_movements_to_loc` (`to_location_id`),
  INDEX `ix_inventory_movements_perf_by` (`performed_by_personnel_id`),
  INDEX `ix_inventory_movements_moved_at` (`moved_at` DESC),
  CONSTRAINT `fk_movements_item`
    FOREIGN KEY (`inventory_item_id`) REFERENCES `inventory_items` (`id`)
    ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT `fk_movements_from_location`
    FOREIGN KEY (`from_location_id`) REFERENCES `store_locations` (`id`)
    ON DELETE RESTRICT ON UPDATE CASCADE,
  CONSTRAINT `fk_movements_to_location`
    FOREIGN KEY (`to_location_id`) REFERENCES `store_locations` (`id`)
    ON DELETE RESTRICT ON UPDATE CASCADE,
  CONSTRAINT `fk_movements_personnel`
    FOREIGN KEY (`performed_by_personnel_id`) REFERENCES `personnel` (`id`)
    ON DELETE RESTRICT ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
