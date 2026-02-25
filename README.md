
### Step A — Clone from GitHub

clone from github


### Step B — Prerequisites

- Windows
- .NET 6 SDK
- SQL Server Express (`SQLEXPRESS` instance)
- SSMS (recommended) or SqlPackage

### Step C — Import the provided database backup (.bacpac)

Provided backup path from your machine:

- `Task Assign.bacpac`


#### Import using SSMS

1. Open SSMS and connect to: `Server=.\SQLEXPRESS`（If you are using a different SQL Server instance name, please update the connection string in appsettings.json accordingly.）
2. Right-click **Databases** → **Import Data-tier Application**
3. Select file: `Task Assign.bacpac`
4. Database name: `Task Assign`
5. Finish the import wizard

### Step D — Verify connection string

In `taskassign/appsettings.json`, confirm:

```json
"DefaultConnection": "Server=.\\SQLEXPRESS;Database=Task Assign;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true"
```

## Step E - Security Notice (SMTP Credentials)

- Need to create a 
- Keep SMTP settings in `taskassign/appsettings.Development.json`, which is ignored by git.
- Never commit real SMTP usernames/passwords into `appsettings.json` or any tracked file.
- If credentials were ever pushed to a public repo, treat them as compromised and **rotate/revoke immediately**.
- Configure SMTP credentials through local secret storage (recommended) or `appsettings.Development.json` (gitignored).

### Step F — Run program

Open `taskassign.sln` in Visual Studio and run using `IIS Express`.

---

## 1) Page-by-Page Features and How to Use

> Default route goes to Task Dashboard; unauthenticated users will be redirected to Login.

### 1.1 Account Pages

1. **Login** (`/Account/Login`)
   - Sign in with Username/Email + Password.
2. **Register** (`/Account/Register`)
   - Create a new account (default role is Member).
3. **Forgot Password** (`/Account/ForgotPassword`)
   - Enter username/email to request password reset OTP.
4. **Verify OTP / Reset Password** (`/Account/VerifyOtp`, `/Account/ResetPassword`)
   - Enter OTP and set a new password.

**Important:** OTP will be sent to the user's email.

### 1.2 Dashboard (`/Task/Dashboard`)

- Shows summary cards such as total tasks, overdue tasks, high-priority tasks, and etc.
- Supports group scope switching.
- Quick entry point to task management and board view.

### 1.3 Task List (`/Task/Index`)

- View task list with pagination.
- Filter by search keyword, status, priority, assignee, and group.
- Supports create/edit/delete operations based on permissions.

### 1.4 Create Task (`/Task/Create`)

- Fill in title, description, due date, status, priority, assignee(only can assign to group member, so pls make sure member accept to join group).
- Save to create task under the active group.

### 1.5 Edit Task (`/Task/Edit/{id}`)

- Update task content and progress.
- Common flow: `InProgress → UAT → Completed → Closed`.

### 1.6 Board (`/Task/Board`)

- Kanban-style view by status columns.
- Status can be changed according to permissions.

### 1.7 Group Page (`/Group/Index`)

- Create groups.
- Invite members by email.
- View groups you can access.
- After login, invited members can open Group page to view pending invites and **Accept/Reject** joining a group.

### 1.8 Accept Invite (`/Group/AcceptInvite?token=...`)

- Invited users can accept/reject group invitation via tokenized URL.

**Important:** Invite URL will be sent to email, and invited users can either:
- handle invites from **Group page after login**, or
- use the **Invite URL link from email** to accept/reject.

---

## 2) Access & Permission Rules (Important)

1. **Task visibility within a group**
   - All members in the same group can view all tasks under that group.

2. **Who can edit / change task status**
   - Only **Group Lead** can edit task content (title, description, due date, assignee, priority).
   - **Assignee** can click Edit in limited mode to submit note/request and update task status only.
   - Group Lead reviews assignee notes/requests (for example, due-date extension requests) and performs the task content update when approved.

---

## 3) Recruiter Quick Validation Flow (5 minutes)

1. Register and log in.
2. Create a group in Group page.
3. Test invite flow:
   - Invite a member and verify invite email URL,
   - Or log in as invited member and accept/reject from Group page.
4. Create 2–3 tasks with different status/priority/due dates.
5. Switch to Board and update task statuses.
6. Validate Dashboard summary changes.


---

