<p align="center">
    <picture>
        <source media="(prefers-color-scheme: dark)" srcset="Ticky.Web/wwwroot/assets/ticky-dark.svg">
        <img src="Ticky.Web/wwwroot/assets/ticky-light.svg" width="300" alt="Ticky logo" title="Ticky logo">
    </picture>
</p>

# Ticky

Ticky is a modern, feature-rich task management system with Kanban-style boards, built using ASP.NET Core Blazor. It is designed to help you manage your projects and tasks efficiently, whether for personal use or team collaboration. Ticky is fully open-source and free to use (and will always be), with a focus on simplicity and usability.

## ❓ The why

You might be thinking, "Another Kanban app?" and that's fair! As someone who navigated between Trello for personal tasks and commercial projects, and Jira at work, I often found myself searching endlessly for "power-ups" that eventually turned into paid features, or trying open-source solutions that didn't quite hit the mark on what I needed or simply lacked the right feel. Ticky was born from that experience - a desire to build something truly comprehensive, intuitive, and always accessible. It's my answer to those frustrations, and it means Ticky will always be free and fully open-source.

## 🌟 Features

Ticky is packed with powerful features designed to make your task management seamless and enjoyable:

- **Projects**: Create and manage projects to group your boards.
- **Boards**: Create and manage your own Kanban boards. You can even make them favorites to stay atop the list.
- **Templates**: You can clone boards, therefore allowing you to use boards as templates.
- **Columns**: Each board can have any amount of columns, all of which are collapsible. You can specify a max card limit, automatically mark the cards within the column as finished and automatically order them.
- **Cards**: Create, edit, and move task cards between columns with drag-and-drop functionality. At a glance see all important information about a card. Decide where new cards are placed.
- **Subtasks**: Break down tasks into smaller, manageable subtasks with completion tracking.
- **Deadline Management**: Set and track deadlines with color-coded indicators.
- **Time Tracking**: Track time spent on tasks with built-in timer functionality. Additionally view how much time has been spent on a specific column in the stats section.
- **Labels and Priorities**: Organize tasks with custom labels and priority levels. Label colors are fully customizable.
- **Attachments**: Upload and manage files associated with tasks.
- **Reminders**: Set email reminders for tasks.
- **Card Linking**: Link related cards together (Jira-like).
- **Activity Tracking**: Monitor all changes and activities on tasks.
- **Comments**: Leave comments on cards to discuss and provide other useful information.
- **User Management**: You can add users on the project level or on the board level, choosing between a Member and an Admin role. Also possible to disable user signups.
- **Admin Panel**: For creating, editing and deleting users as an admin. Mostly for when not using SMTP.
- **Email Notifications**: Receive notifications for deadlines, and reminders.
- **Progress**: Track your progress within a board by seeing how many tasks have already been completed.
- **App-wide Search**: Find cards from other boards based on their unique identificator (like TEST-1), jump directly to them.
- **Recent board**: Immediately go back to your most recent board.
- **Auto-generated avatars** (optional): To make things more colorful.
- **Able to do run offline**: Ability to run fully offline, disabling the avatar service, having all the files bundled on the server and not using SMTP.
- **Dark Mode**: A sleek dark mode for less strain on your eyes.
- **Snooze Cards**: Reduce clutter by snoozing cards that cannot be worked on just yet.
- **Repeat Cards**: Automatically repeat cards at specified intervals, perfect for recurring tasks.
- **Responsive Design**: Take your tasks with you, no matter whether you are at your computer or running errands with just your phone.
- **Completion Confetti**: Feel the satisfaction of completing each task with a bunch of confetti, there to celebrate your success.
- **Filtering**: Easily find and organize tasks based on various criteria.
- **Trello import**: You can import your Trello boards, including the ability to map all the assigned members from your Trello board to Ticky users.
- **API**: Let scripts and agents do everything you can do in the app, with API tokens that are read-only or read and write. See [API](#-api).
- ... and more!

## 📋 Prerequisites

- **Docker** installed on your system
  - Windows: Download and install Docker Desktop from [docker.com](https://www.docker.com/products/docker-desktop/)
  - macOS: Download and install Docker Desktop from [docker.com](https://www.docker.com/products/docker-desktop/)
  - Linux: Install Docker Engine and Docker Compose via your package manager
- **Basic text editor** (Notepad, VS Code, etc.) to create/edit the configuration file
- **SMTP Server** (optional) - for email notifications and password resets
  - Gmail, Outlook, or any email provider that supports SMTP
  - If you don't have SMTP, Ticky will work without email features

## 🚀 Getting Started

### Using Docker (Recommended)

**Step 1: Verify Docker Installation**
Open a terminal/command prompt and run:

```bash
docker --version
```

If these commands work, you're ready to proceed!

**Step 2: Create Project Directory**
Create a new folder for Ticky on your computer:

```bash
mkdir ticky-app
cd ticky-app
```

**Step 3: Create Configuration File**
Create a new file called `docker-compose.yaml` in your project folder and copy the following content. **Important**: Replace `your-secure-password` with a strong password of your choice (use the same password in all places where it appears):

```yaml
services:
  ticky-app:
    image: ghcr.io/dkorecko/ticky:latest # or pin to a specific version (like v1.0.0) for manual updates
    container_name: ticky-app
    ports:
      # Behind a reverse proxy, don't publish this port: remove the whole "ports" entry if the proxy runs in the same Compose network (it reaches ticky-app:8080),
      # or use "127.0.0.1:4088:8080" if the proxy runs on the same host.
      - "4088:8080"
    restart: unless-stopped
    volumes:
      - ./data/app/uploaded:/app/wwwroot/uploaded
    environment:
      - DB_HOST=ticky-db
      - DB_NAME=ticky # Database name, can be customized
      - DB_USERNAME=ticky # Database username, can be customized
      - DB_PASSWORD=your-secure-password
      #- FULLY_OFFLINE=true # Uncomment this if you want to disable the avatar service and run fully offline.
      #- DISABLE_USER_SIGNUPS=true # Uncomment to disable user self-registration. When true, only admins can create new users via the Admin Panel.
      - BASE_URL=http://localhost:4088 # Base URL for generating clickable links in emails (e.g., reminder emails). Change to the base URL you use to access Ticky.
      #- API_RATE_LIMIT=600 # API requests each user may make per minute, across all their tokens. Set to 0 for no limit.
      - SMTP_ENABLED=true # Change this to false to ignore SMTP configuration and disable SMTP setup. Resetting password via typical password reset won't work (will need to be reset by an admin via the Admin Panel), as well as reminders and notifications. Can be enabled at any time.
      - SMTP_HOST=your-smtp-host
      - SMTP_PORT=your-smtp-port
      - SMTP_DISPLAY_NAME=Ticky
      - SMTP_EMAIL=your-email@example.com
      - SMTP_USERNAME=your-smtp-username
      - SMTP_PASSWORD=your-smtp-password
      - SMTP_SECURITY=true
      # Behind an HTTPS reverse proxy that sends X-Forwarded-Proto (most do; Nginx needs "proxy_set_header X-Forwarded-Proto $scheme;"), Ticky trusts the X-Forwarded-Proto/-For headers so the sign-in cookie is marked Secure.
      # This is on by default in the image, and nothing changes over plain HTTP without that header. Sign in again once over HTTPS to get a Secure cookie. Uncomment to turn it off.
      #- ASPNETCORE_FORWARDEDHEADERS_ENABLED=false
    depends_on:
      ticky-db:
        condition: service_healthy
  ticky-db:
    image: mysql:8
    container_name: ticky-db
    restart: unless-stopped
    environment:
      MYSQL_DATABASE: ticky # This should match DB_NAME in ticky-app container
      MYSQL_USER: ticky # This should match DB_USERNAME in ticky-app container
      MYSQL_ROOT_PASSWORD: your-secure-password
      MYSQL_PASSWORD: your-secure-password # This should match DB_PASSWORD
    healthcheck:
      test: ["CMD", "mysqladmin", "ping", "-h", "localhost"]
      timeout: 2s
      retries: 30
    volumes:
      - ./data/mysql:/var/lib/mysql
```

**Step 4: Configure Email (Optional)**
If you want email notifications and password reset functionality:

- Replace `your-smtp-host`, `your-smtp-port`, `your-email@example.com`, etc. with your actual email provider settings
- For Gmail: Use `smtp.gmail.com`, port `587`, and create an [App Password](https://support.google.com/accounts/answer/185833)
- If you don't want email features, change `SMTP_ENABLED=true` to `SMTP_ENABLED=false`

**Step 5: Start Ticky**
In your terminal/command prompt, navigate to your project folder and run:

```bash
docker compose up -d
```

This will:

- Download the necessary Docker images (this may take a few minutes the first time)
- Create and start the Ticky application and database
- Set up data storage folders automatically

**Step 6: Access Your Ticky Instance**

1. Open your web browser and go to: `http://localhost:4088`
2. Log in with the default admin account:
   - **Email**: `admin@ticky.com`
   - **Password**: `abc123`
3. You'll be prompted to change these credentials immediately for improved security
4. After changing your password, you'll be logged out - just log back in with your new credentials

**Step 7: Add Users (If SMTP Disabled)**
If you disabled SMTP, you'll need to create user accounts manually through the Admin Panel. If SMTP is enabled, users can register themselves.

### Troubleshooting

- **Port already in use**: Change `4088:8080` to `4089:8080` (or any other available port) in the docker-compose.yaml file
- **Permission errors**: Make sure Docker Desktop is running and you have proper permissions. In Linux, sudo can be used in front of the commands.
- **Can't access the app**: Wait a minute after starting - the database needs time to initialize on first run

### Manual Setup

1. Clone the repository:

   ```
   git clone https://github.com/dkorecko/Ticky.git
   cd Ticky
   ```

2. Set up your environment variables.

3. Run the application:
   ```
   dotnet run --project Ticky.Web/Ticky.Web.csproj
   ```

## 📷 Preview

![Image of the landing page in light mode](images/landing_light.png)
![Image of the board in light mode](images/board_light.png)
![Image of the card in light mode](images/card_light.png)
![Image of the landing page in dark mode](images/landing_dark.png)
![Image of the board in dark mode](images/board_dark.png)
![Image of the card in dark mode](images/card_dark.png)

## 🤝 Community

If you have any questions, need help setting up, want to share your feedback or discuss ideas and new features, then I have created a [Discord](https://discord.gg/DHCZqYwUUb) server. I’m always eager to hear from users and improve Ticky based on your needs.

## 🛠️ Project Structure

- **Ticky.Base**: Core entities, models, and shared components.
- **Ticky.Internal**: Data access, services, and business logic.
- **Ticky.Web**: Blazor web application, UI components, and user interface.

## 🔧 Configuration

### Database Setup

The application automatically applies migrations on startup.

## 🔌 API

Ticky has a REST API under `/api/v1`, so scripts and AI agents can work with Ticky end to end: everything you can do in the app, under the same rules. Every request acts as the user who created the token, so it reaches exactly the boards that user can.

AI agents that can run shell commands should read the short guide written for them first: every instance serves it at `/api/agent-guide.md`, and it lives in the repository as [`Ticky.Web/Docs/AgentGuide.md`](Ticky.Web/Docs/AgentGuide.md). Point your agent at it, together with a token.

### Tokens

Create a token in **Settings → API tokens**. Choose whether it's **Read only** (only the `GET` endpoints) or **Read and write**, and when it expires. The token is shown once, since Ticky stores only a hash of it. Revoking it there stops it working right away.

Send the token with every request:

```bash
curl -H "Authorization: Bearer tky_..." http://localhost:4088/api/v1/boards
```

### Endpoints

Every Ticky instance serves the full reference at `/api/docs`, where you can also try the endpoints, and the OpenAPI document at `/openapi/v1.json` for API clients, code generators and agent tools. Both work offline. In short:

| Area | Endpoints |
| --- | --- |
| **You** | `GET /me` who the token acts as · `GET /me/work` your open assigned cards and running timers · `GET /search?q=` cards on all your boards by name or key, like `TKY-12` |
| **Projects** | `GET`, `POST /projects` · `GET`, `PATCH`, `DELETE /projects/{id}` · `POST /projects/{id}/boards` · members: `GET`, `POST /projects/{id}/members`, `PATCH`, `DELETE /projects/{id}/members/{userId}` |
| **Boards** | `GET /boards` · `GET`, `PATCH`, `DELETE /boards/{id}` · `GET /boards/{id}/cards` with filters · `GET /boards/{id}/activity` · `POST /boards/{id}/clone` · `PUT`, `DELETE /boards/{id}/favorite` · members: `GET`, `POST /boards/{id}/members`, `PATCH`, `DELETE /boards/{id}/members/{userId}` |
| **Columns and labels** | `POST /boards/{id}/columns` · `PATCH`, `DELETE /columns/{id}` · `POST /columns/{id}/move` · `POST /boards/{id}/labels` · `PATCH`, `DELETE /labels/{id}` |
| **Cards** | `POST /cards` · `GET`, `PATCH`, `DELETE /cards/{id}` · `POST /cards/{id}/move` · `GET /cards/{id}/activity` · `POST /cards/{id}/comments` · `PUT`, `DELETE /cards/{id}/assignees/{userId}` · `PUT`, `DELETE /cards/{id}/labels/{labelId}` · `PUT`, `DELETE /cards/{id}/snooze` · `PUT`, `DELETE /cards/{id}/repeat` |
| **Card items** | subtasks: `POST /cards/{id}/subtasks`, `PATCH`, `DELETE /cards/{id}/subtasks/{subtaskId}`, `POST …/move`, `PUT`, `DELETE …/assignees/{userId}` · time: `PUT`, `DELETE /cards/{id}/timer` (start and stop yours), `POST /cards/{id}/time-records`, `PATCH`, `DELETE /cards/{id}/time-records/{recordId}` · `POST /cards/{id}/reminders`, `DELETE …/{reminderId}` · attachments: `POST /cards/{id}/attachments` (tokens only), `GET …/{attachmentId}/content`, `DELETE …/{attachmentId}` · links: `POST /cards/{id}/links`, `DELETE …/{linkId}` |

All paths start with `/api/v1`. For example, to create a card with a label and an assignee, and then move it to another column:

```bash
curl -X POST http://localhost:4088/api/v1/cards \
  -H "Authorization: Bearer tky_..." -H "Content-Type: application/json" \
  -d '{"columnId": 12, "name": "Renew the domain", "priority": "High", "labelIds": [3], "assigneeIds": [7]}'

curl -X POST http://localhost:4088/api/v1/cards/345/move \
  -H "Authorization: Bearer tky_..." -H "Content-Type: application/json" \
  -d '{"targetColumnId": 13}'
```

### Permissions

The API follows the app's rules. Members of a board, or of its project, can do everything with its cards. Board admins also change the board, its columns, labels and members. Project admins change the project and its members, and add boards to it. Anyone can create a project. Read-only tokens can only read.

### Conventions

- **Times** are in the server's time zone (in Docker that's UTC unless you set `TZ`) and are returned without an offset. Times you send with an offset or `Z` are converted to it. Durations are `hh:mm:ss`.
- **Enums** are names, like `"priority": "High"`.
- **Partial updates** (`PATCH`) change only the fields you send. Adding something a card already has, or removing something it doesn't, changes nothing and succeeds.
- **Errors** are [problem details](https://www.rfc-editor.org/rfc/rfc9457): `401` without a valid token, `403` when a read-only token calls a write endpoint or you may not make the change (e.g. you aren't an admin), `404` for anything that doesn't exist or that you can't access, and `400` or `409` with a `detail` or per-field `errors` when a request can't be done.
- **Activity** comes a page at a time, newest first: pass a page's `nextBefore` as `before` to get the next one.
- **Rate limit**: each user may make 600 requests a minute across all their tokens, or what `API_RATE_LIMIT` sets (`0` turns the limit off). Past it, requests get `429` with a `Retry-After` header.
- **Changes** made through the API show up on open boards right away and appear in the card's activity as yours.

### Live updates

To hear about changes instead of polling, connect a [SignalR](https://learn.microsoft.com/aspnet/core/signalr/introduction) client to `/updatehub` with your token, either in the `Authorization` header or as the `access_token` query parameter. Call `SubscribeBoard(boardId)` for each board, and handle `BoardChange(boardId, pageId)`: it tells you the board changed, so you can load what you need again.

## 🧪 Development

### Building

```
dotnet build
```

### Running Tests

```
dotnet test
```

### Watch Mode

```
dotnet watch
```

## 🔄 CI/CD

The project includes GitHub Actions workflows for CI/CD:

- Automated builds and tests on pull requests
- Docker image publishing to GitHub Container Registry (GHCR) on releases

## 🤖 AI Statement

Ticky was built by hand, long before AI coding tools were part of my workflow. These days I do use AI to help with development, but nothing gets merged without me reviewing every line of code myself. I'm a professional senior software engineer with 12 years of coding experience, and I hold all code in Ticky to the same standard, whoever (or whatever) wrote the first draft.

## 📝 Contributing

Contributions are welcome! See the [CONTRIBUTING.md](CONTRIBUTING.md) file for details.

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 👏 Acknowledgements

- [Blazor](https://dotnet.microsoft.com/apps/aspnet/web-apps/blazor) - Web framework used
- [Entity Framework Core](https://docs.microsoft.com/en-us/ef/core/) - ORM used
- [TailwindCSS](https://tailwindcss.com/) - CSS framework
- [Font Awesome](https://fontawesome.com/) - Icons
- [Sortable.js](https://github.com/SortableJS/Sortable) - Drag-and-drop functionality

## 📞 Contact

Have questions or feedback? I'd love to hear from you! Please feel free to open an issue on this repository.

---

Made with ❤️
