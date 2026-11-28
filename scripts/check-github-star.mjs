import { execFileSync } from 'node:child_process';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';

const scratch = fs.mkdtempSync(path.join(os.tmpdir(), 'cybergems-github-star-'));
const source = path.resolve('src/CyberWall.UI/Services/GitHubStarService.cs');
const project = path.join(scratch, 'GitHubStarSmoke.csproj');
const program = `using CyberWall.UI.Services;
static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
int opened = 0;
Action open = () => opened++;
Check(await GitHubStarService.RunOperationAsync(() => Task.FromResult(true), open) == GitHubStarResult.Starred, "CLI success must confirm the star");
Check(opened == 0, "CLI success must keep the browser closed");
Check(await GitHubStarService.RunOperationAsync(() => Task.FromResult(false), open) == GitHubStarResult.Opened, "Missing or unauthenticated CLI must open GitHub");
Check(opened == 1, "The fallback must open once");
Check(await GitHubStarService.RunOperationAsync(() => Task.FromResult(false), () => throw new Exception("Browser unavailable")) == GitHubStarResult.Failed, "Browser failures must stay failures");
Check(await GitHubStarService.RunOperationAsync(() => throw new Exception("Unexpected CLI error"), open) == GitHubStarResult.Failed, "Unexpected failures must be reported");
Check(await GitHubStarService.RunOperationAsync(() => Task.FromResult(true), open) == GitHubStarResult.Starred, "A failed request must allow retry");
Console.WriteLine("GitHub star smoke passed: confirmed success, browser fallback, failures and retry");
`;
try {
  fs.writeFileSync(project, `<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup><ItemGroup><Compile Include="${source}" Link="GitHubStarService.cs" /></ItemGroup></Project>`);
  fs.writeFileSync(path.join(scratch, 'Program.cs'), program);
  execFileSync('dotnet', ['run', '--project', project, '--no-launch-profile', '--verbosity', 'quiet'], { stdio: 'inherit' });
} finally {
  if (path.dirname(path.resolve(scratch)) !== path.resolve(os.tmpdir())) throw new Error('Unexpected test directory');
  fs.rmSync(scratch, { recursive: true, force: true });
}
