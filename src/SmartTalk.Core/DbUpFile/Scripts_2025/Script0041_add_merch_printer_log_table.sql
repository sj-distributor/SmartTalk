CREATE TABLE `merch_printer_log` (
                                     `id` char(36) NOT NULL,
                                     `agent_id` int not null,
                                     `order_id` char(36) DEFAULT NULL,
                                     `printer_mac` varchar(64) NOT NULL,
                                     `print_log_type` tinyint(1) unsigned NOT NULL,
                                     `message` varchar(255) DEFAULT NULL,
                                     `code` smallint(6) DEFAULT NULL,
                                     `code_description` varchar(255) DEFAULT NULL,
                                     `created_date` datetime(3) NOT NULL,
                                     PRIMARY KEY (`id`) USING BTREE,
                                     KEY `idx_merch_id_created_date` (`agent_id`,`created_date`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;