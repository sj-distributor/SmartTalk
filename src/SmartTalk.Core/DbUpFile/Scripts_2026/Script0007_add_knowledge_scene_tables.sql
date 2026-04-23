create table if not exists knowledge_scene_folder
(
    `id` int primary key auto_increment,
    `name` varchar(128) not null,
    `created_at` datetime(3) not null,
    `updated_at` datetime(3) null
    ) charset=utf8mb4;

create table if not exists knowledge_scene
(
    `id` int primary key auto_increment,
    `folder_id` int not null,
    `name` varchar(128) not null,
    `description` varchar(2048) null,
    `status` int not null,
    `created_at` datetime(3) not null,
    `updated_at` datetime(3) null
    ) charset=utf8mb4;

create table if not exists knowledge_scene_knowledge
(
    `id` int primary key auto_increment,
    `scene_id` int not null,
    `name` varchar(128) not null,
    `type` int not null,
    `content` text null,
    `file_name` varchar(255) null,
    `created_at` datetime(3) not null,
    `updated_at` datetime(3) null
    ) charset=utf8mb4;

create table if not exists ai_speech_assistant_knowledge_scene_relation
(
    `id` int primary key auto_increment,
    `knowledge_id` int not null,
    `scene_id` int not null,
    `created_at` datetime(3) not null
    ) charset=utf8mb4;