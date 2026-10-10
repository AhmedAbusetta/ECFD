import * as signalR from "@microsoft/signalr";

const SIGNALR_URL = process.env.NEXT_PUBLIC_BACKEND_SIGNALR_URL || "http://localhost:5000/hubs/dashboard";

export class SignalRService {
  private connection: signalR.HubConnection | null = null;
  private stopped = false;

  public startConnection(
    onCallStarted: (data: any) => void,
    onTranscriptFinal: (data: any) => void,
    onTacticDetected: (data: any) => void,
    onStageChanged: (data: any) => void,
    onRiskUpdated: (data: any) => void,
    onAlertRaised: (data: any) => void,
    onCallEnded: (data: any) => void,
    onAnalystUpdated?: (data: any) => void,
    onTranscriptPartial?: (data: any) => void,
    onVoiceUpdated?: (data: any) => void,
    onEmployeeWarned?: (data: any) => void
  ) {
    this.connection = new signalR.HubConnectionBuilder()
      .withUrl(SIGNALR_URL)
      .withAutomaticReconnect()
      .build();

    this.connection.on("call.started", onCallStarted);
    this.connection.on("transcript.final", onTranscriptFinal);
    this.connection.on("tactic.detected", onTacticDetected);
    this.connection.on("stage.changed", onStageChanged);
    this.connection.on("risk.updated", onRiskUpdated);
    this.connection.on("alert.raised", onAlertRaised);
    this.connection.on("call.ended", onCallEnded);
    if (onAnalystUpdated) this.connection.on("analyst.updated", onAnalystUpdated);
    if (onTranscriptPartial) this.connection.on("transcript.partial", onTranscriptPartial);
    if (onVoiceUpdated) this.connection.on("voice.updated", onVoiceUpdated);
    if (onEmployeeWarned) this.connection.on("employee.warned", onEmployeeWarned);

    this.stopped = false;
    this.connectWithRetry(this.connection);
  }

  // withAutomaticReconnect only covers connections that were up once; if the backend isn't running yet
  // when the dashboard opens (or is restarting), keep trying so real calls still show up.
  private async connectWithRetry(connection: signalR.HubConnection) {
    for (let attempt = 0; !this.stopped && connection === this.connection; attempt++) {
      try {
        await connection.start();
        console.log("[SignalR] Connected to ECFD Dashboard Hub");
        return;
      } catch (err) {
        if (attempt === 0) console.warn("[SignalR] Backend not reachable yet, retrying every 3 s…", err);
        await new Promise((resolve) => setTimeout(resolve, 3000));
      }
    }
  }

  public stopConnection() {
    this.stopped = true;
    if (this.connection) {
      this.connection.stop();
    }
  }
}

export const signalRService = new SignalRService();
