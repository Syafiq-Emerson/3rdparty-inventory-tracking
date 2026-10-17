-- =====================================================================
-- Industrial Inventory Management System - MySQL 8.0+ Production Schema
-- Company IT Standard Database Definition
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

-- ---------------------------------------------------------------------
-- Baseline Seed Data
-- ---------------------------------------------------------------------
INSERT INTO `store_locations` (`id`, `location_code`, `location_name`, `is_active`, `created_at`, `updated_at`) VALUES
('loc-001', 'WH-A-01', 'Warehouse A - Bay 1 (Heavy Machinery)', 1, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)),
('loc-002', 'WH-B-03', 'Warehouse B - Rack 3 (Valves & Actuators)', 1, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)),
('loc-003', 'CAL-LAB-01', 'Calibration Lab - Clean Room A', 1, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)),
('loc-004', 'FAB-SHOP-2', 'Fabrication Shop - Assembly Zone 2', 1, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)),
('loc-005', 'HOLD-INSP', 'Receiving Dock - Quarantine & Inspection', 1, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)),
('loc-006', 'DECOMM-BAY', 'Decommissioned Equipment Holding', 0, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6))
ON DUPLICATE KEY UPDATE `location_code` = `location_code`;

INSERT INTO `personnel` (`id`, `personnel_code`, `name`, `is_active`, `created_at`, `updated_at`) VALUES
('per-001', 'EMP-1042', 'Marcus Vance (Lead Instrumentation Tech)', 1, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)),
('per-002', 'EMP-1189', 'Elena Rostova (Quality Assurance Specialist)', 1, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)),
('per-003', 'EMP-2055', 'David Kim (Materials Handling Supervisor)', 1, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)),
('per-004', 'EMP-3120', 'Sarah Jenkins (Calibration Engineer)', 1, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)),
('per-005', 'EMP-9001', 'Arthur Pendelton (Contractor - Retired)', 0, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6))
ON DUPLICATE KEY UPDATE `personnel_code` = `personnel_code`;

INSERT INTO `inventory_items` (`id`, `serial_number`, `tag_number`, `location_id`, `registered_by_personnel_id`, `created_at`, `updated_at`) VALUES
('item-001', 'F005327261', 'PV-16501', 'loc-002', 'per-001', UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)),
('item-002', 'F005327262', 'FT-10402', 'loc-001', 'per-002', UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)),
('item-003', 'F005327289', 'LT-20415', 'loc-003', 'per-004', UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)),
('item-004', 'S991048210', 'PT-33018', 'loc-004', 'per-003', UTC_TIMESTAMP(6), UTC_TIMESTAMP(6))
ON DUPLICATE KEY UPDATE `serial_number` = `serial_number`;

INSERT INTO `inventory_movements` (`id`, `inventory_item_id`, `from_location_id`, `to_location_id`, `performed_by_personnel_id`, `event_type`, `moved_at`) VALUES
('mov-001', 'item-001', NULL, 'loc-002', 'per-001', 'INITIAL_REGISTRATION', UTC_TIMESTAMP(6)),
('mov-002', 'item-002', NULL, 'loc-001', 'per-002', 'INITIAL_REGISTRATION', UTC_TIMESTAMP(6)),
('mov-003', 'item-003', NULL, 'loc-003', 'per-004', 'INITIAL_REGISTRATION', UTC_TIMESTAMP(6)),
('mov-004', 'item-004', NULL, 'loc-004', 'per-003', 'INITIAL_REGISTRATION', UTC_TIMESTAMP(6))
ON DUPLICATE KEY UPDATE `id` = `id`;
