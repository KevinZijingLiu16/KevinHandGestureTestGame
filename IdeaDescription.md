This prototype started with a question during Raine Koskimaa’s lecture, as we discussed Thomas Apperley’s chapter “Understanding Players”: how might changing the way we control a game change the experience of playing it?

I built a small Unity prototype using webcam-based hand tracking. Moving my hand controls the character, a quick upward movement triggers a jump, closing my fist fires a projectile, and holding a V sign produces a continuous beam. Both attacks consume energy, adding a resource-management element alongside movement and aiming.

Under the hood, I used Unity’s WebCamTexture API to capture the camera feed and Google’s MediaPipe Hand Landmarker, integrated through homuler’s open-source MediaPipeUnityPlugin, to track 21 hand landmarks locally. My own C# logic translates hand position, movement, and finger curl into gameplay actions, with smoothing and gesture confirmation to help reduce accidental inputs.

Apperley’s discussion of play as embodied and materially situated gave me a useful way to think about these interactions. Keyboard and controller play already involve the body. This prototype explores how different movements might reshape the coordination, effort, and sense of control involved in playing.

One design choice keeps me thinking: the same hand handles movement, aiming, and attacking. How will players learn to coordinate those actions? When might that feel expressive, and when might it become tiring or frustrating?

It also makes me question what I mean by “intuitive.” A gesture that makes sense to me may feel unfamiliar or uncomfortable to someone else. Camera placement, lighting, and the space available to move also become part of the conditions of play.

The chapter’s emphasis on observing players gives me a direction for exploring these questions: watching how people adapt their movements, where they hesitate, and how they describe their sense of control. Getting the system to recognise a gesture is one step; understanding how someone experiences that interaction requires listening to and learning from players.

Building this prototype has helped me turn a classroom discussion into concrete design questions—and examine the assumptions I make about the person playing.

#GameDesign #GameStudies #Unity #Prototyping #HumanComputerInteraction
