# Your Role: Task Master

## Your "Guiding Instructions":

	```		
		Hello! Thank you for your time we're so excited you're working for us! Happy happy!
		
		SUBAGENTs:
		 - Coding and General Development
		 - Critical Peer Review
		 - Research
		
		TASK MASTER:
			All tasks are DONE and WORKED ON by SUBAGENTS. You are simply their task master.
		
			Steps for each task:
			 1. A new SUBAGENT instance reports back to you a maximum 2 paragraphs of context history regarding the task.
			 2. A new SUBAGENT instance does the work, you include instructions from the task + research from Step 1 + any useful instructions you decide the subagent may need. 
			 3. A new SUBAGENT instance critically reviews the work done from Step 2. You decide if we must go back to Step 2 given Step 3 feedback.
			 4. If SUBAGENT Step 3 reports no found issues and task adherence is fine, you flip in "current_goals.md" "DONE? NO!" to "DONE? YES!", a new SUBAGENT instance writes the commit for the task, including current_goals.md, and pushes.
		 
			References to variables like X and Y automatically imply that these are to be exposed properties for developer tweaking.
		
		SKILLS TO KNOW: for you and/or subagents:
			box-agent-playtest
			sbox-live-play-agent-loop
			sbox-editor-mcp-driving

		SIGNAL SERVER CLI: I may not be available at my pc, if clarification questions time out once, pass on the message to signal cli.
	```	

---


# WORKING DATA

SESSION START? YES!
	
TASKS:

	Task 1: DONE? NO!
		The sun -> moon transition is instant, not a fade.

	Task 0: DONE? YES!
		Ther is a moon object in the project, already correct distance from origin. Just need it to be equal light source angle when the DayNightCycle code is in night mode, the moon rising as the sun sets.
		Result: the light follows an arc that peaks at Elevation Degrees (Degree 0 noon, 0.25 sunset, 0.5 midnight, 0.75 sunrise). The moon object orbits opposite the sun at its authored distance (20000), same face toward the origin, so it rises as the sun sets; at night sun-moon-light takes the moon's angle. Settings: CODE > DayNightCycle > Moon (wired to "moon"); Elevation Degrees is now the peak altitude (your 5 = a low arc). Until Task 1 the day/night switch at the horizon is hard.
		Evidence: offline gate 0 errors; live play probe 10/10 rows (light disc on the moon at night and opposite it by day, moon distance 20000, face toward origin); moon renders in play at Degree 0.3 and 0.5; review 0 HIGH, 1 MEDIUM fixed (night is tested on the cosine, so a 0 peak still has nights).

NOTES:


CLARIFICATION Q&A:

AVAILABLE? No, use signal for clarification answers.

Q1 (Task 0): The light stays at a fixed elevation (_ElevationDegrees = 5 in the scene) and only spins its yaw, so nothing rises or sets. How should sun and moon move?
A1: Arc: elevation rises and falls over the cycle (peak = _ElevationDegrees, yaw keeps sweeping _SweepDegrees). The moon object always sits opposite the sun at its 20000 distance, so it rises east as the sun sets west; at night the one sun-moon-light takes the moon's angle (sky disc + shadows from the moon).

Q2 (Task 0): The play camera (prefabs/maincamera.prefab) had ZFar 5000; the moon at 20000 (radius ~5000, ~28 degrees wide) is clipped in play. What should change?
A2: "Don't worry, did this myself now." (editor at 22:58: maincamera prefab session ZFar 30000, unsaved; disk still 5000)

Q3 (Task 1): Cause: g_flNightBlend and the light colour come straight from Degree (0..1), so the sky fades day to night over the whole cycle, then snaps night to day when Degree wraps 1 to 0. How should the transition work?
A3: Fade window at each horizon crossing (sunset and sunrise), width an exposed property (default 10% of the cycle, ~34 s at 338 s). Sky blend and light colour fade there, and the light dims (never black) to hide its sun-to-moon angle swap at the horizon.

Q4 (all): DayNightCycle.cs is not in HEAD; shader/.shader_c/.vmat, scene, GameManager.cs, sbproj and the Assets/ -> prefabs/, models/ move are uncommitted. How should commits go?
A4: First one baseline commit of all that current work exactly as it is now, then one commit per task on top; push each, per the doc.

Q5 (all): May one driver subagent use the open Dev editor (pid 417666)?
A5: Yes, one driver only: hotload, wire + save the scene, short play runs with Degree jumped to the horizon crossings, screenshots, then play stop. It backs off if you start playing.

Parent decisions (change if wrong):
- Questions went to the PC clarify card, not Signal: the doc was saved at 22:52 and the goal came from the PC at 22:53 (header: Signal only after a clarify timeout).
- Header skill names read as: box-agent-playtest = sbox-agent-playtest; sbox-editor-mcp-driving = sbox-cli-driving (the editor HTTP MCP was removed upstream).
- Order: Task 0, then Task 1, one at a time (both rewrite DayNightCycle.Apply; Task 1's fades sit on Task 0's horizon crossings). Commits: baseline, Task 0, Task 1.
- The baseline waits until your open editor edits are saved (unsaved at 22:58: basic_example.scene and prefabs/maincamera.prefab). current_goals.md rides in the Task 0 commit, not the baseline.
- Moon: new DayNightCycle property _Moon (GameObject), wired to the scene's "moon". Orbit centre = world origin; radius = the moon's authored distance from origin, read at start (20000 today).
- The moon turns with its orbit so the same face points at the origin, keeping the child Spot Light's authored offset. Flag, not changed: that Spot Light sits 11000 units beyond the moon on the far side and shines toward the origin, so it (and the moonlight) light the moon's far face; the face players see gets only ambient. Say if it should move in front.
- The moon stays opposite the sun by day too (below the horizon), not hidden.
- _ElevationDegrees becomes the peak altitude (noon and midnight); key kept, so your value 5 gives a 5-degree peak. _SweepDegrees still means yaw per full cycle. Degree 0 stays full day (noon) as today; 0.25 sunset, 0.5 midnight, 0.75 sunrise.
- Fade: new property for the window width (fraction of the cycle, default 0.1) and one for the dim twilight floor of the light colour at the swap; never pure black (black = "sun off" and the sky loses the light direction).
- The sky shader's own 2.2 degree moon disc stays; at night it sits behind the 3D moon and its halo glows around the rim. To drop it, set Moon Brightness 0 on materials/night_sky.vmat. No shader change.
- The driver never saves while your sessions hold unsaved changes (save_scene would write your in-memory edits too); it stops and reports instead.
- Push target: remote/main (github CSEliot/labyrinth). The first push also sends your commit "Fu" (b92f46d).
- models/moon/moon.vmdl is not under Assets/ (resolves in the editor, likely a downloaded cloud asset); left as is.

---


# IGNORE

/goal Update labyrinth project state according to document current_goals.md. If "SESSION START?" is "NO!" then consider best clarifying questions, ask them, and record them below and after that update "SESSION START?" to "YES!"

Checklist:
Session start == ?
Task Done? No/Yes?
Notes and Clarification sections empty?
Update [project-name] == correct project name?
Skills to know, if any, are correct?
