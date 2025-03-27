create table if not exists `linphone_cdr`
(
    `id` int primary key auto_increment,
    `call_date` bigint not null,
    `caller` varchar(150) not null,
    `targetter` varchar(150) not null,
    `status` int not null,
    `agent_id` int not null,
    `created_date` datetime(3) not null
)charset=utf8mb4;

create table if not exists `linphone_sip`
(
    `id` int primary key auto_increment,
    `agent_id` int not null,
    `sip` varchar(150) not null,
    `created_date` datetime(3) not null
)charset=utf8mb4;