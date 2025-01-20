alter table `restaurant_asterisk`
    add column `host_id` int null;

alter table `restaurant_asterisk`
    add column `endpoint` varchar(255) null;

alter table `restaurant_asterisk`
    add column `host_records` varchar(255) null;

alter table `restaurant_asterisk`
    change column `cdr_domain_name` `domain_name` varchar(125) not null;