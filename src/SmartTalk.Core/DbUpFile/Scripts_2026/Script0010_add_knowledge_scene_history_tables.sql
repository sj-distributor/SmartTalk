alter table knowledge_scene
    add column `version` varchar(128) not null default '1.0',
    add column `is_active` tinyint(1) not null default 1;

create table if not exists knowledge_scene_history
(
    `id` int primary key auto_increment,
    `scene_id` int not null,
    `folder_id` int not null,
    `name` varchar(128) not null,
    `description` varchar(2048) null,
    `version` varchar(128) not null,
    `status` int not null,
    `is_active` tinyint(1) not null,
    `created_at` datetime(3) not null,
    `updated_at` datetime(3) null,
    `snapshot_at` datetime(3) not null
) charset=utf8mb4;

create table if not exists knowledge_scene_history_item
(
    `id` int primary key auto_increment,
    `history_id` int not null,
    `scene_item_id` int null,
    `name` varchar(128) not null,
    `type` int not null,
    `content` text null,
    `file_name` varchar(255) null,
    `created_at` datetime(3) not null,
    `updated_at` datetime(3) null
) charset=utf8mb4;
