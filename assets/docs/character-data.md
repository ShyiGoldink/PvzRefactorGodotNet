# 角色数据约定

剧情里每条对话都有一个 `speaker_id`（"谁在说"），它指向的就是这里配的角色。

---

## 1. 存放位置与命名

```
resource/data/characters/<角色编号>.json
resource/data/characters/<角色编号>.png
```

例：角色 1 → `resource/data/characters/1.json`

---

## 2. 字段

| 字段 | 类型 | 说明 |
|---|---|---|
| `id` | int | 角色编号，和文件名一致。剧情里的 `speaker_id` 就是它 |
| `display_name` | string | 显示名 |
| `portrait` | string | 立绘 / 头像的资源路径 |
| `animations` | object | 动画表，见下 |

---

## 3. 动画表（animations）

以后要给角色做动画，都挂在 `animations` 下面：

```json
"animations": {
  "idle":   { },
  "talk":   { }
}
```

**结构还没定**（用什么做动画、一段动画要配哪些参数，等真做动画的时候再说），
现在先留一个空对象占位，保证字段不缺失——转 protobuf 的时候空字段和缺字段是两回事。
