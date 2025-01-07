alter table `restaurant_asterisk`
    add column `host_id` int null;

alter table `restaurant_asterisk`
    add column `endpoint` varchar(255) null;

alter table `restaurant_asterisk`
    add column `host_records` varchar(255) null;