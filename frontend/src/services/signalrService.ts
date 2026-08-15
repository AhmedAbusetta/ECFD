import * as signalR from "@microsoft/signalr";

const SIGNALR_URL = process.env.NEXT_PUBLIC_BACKEND_SIGNALR_URL || "http://localhost:5000/hubs/dashboard";

export class SignalRService {
  private connection: signalR.HubConnection | null = null;

  public startConnection(
    onCallStarted: (data: any) => void,
    onTranscriptFinal: (data: any) => void,
    onTacticDetected: (data: any) => void,
    onStageChanged: (data: any) => void,
    onRiskUpdated: (data: any) => void,
    onAlertRaised: (data: any) => void,
    onCallEnded: (data: any) => void
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

    this.connection
      .start()
      .then(() => console.log("[SignalR] Connected to ECFD Dashboard Hub"))
      .catch((err) => console.error("[SignalR] Connection Error: ", err));
  }

  public stopConnection() {
    if (this.connection) {
      this.connection.stop();
    }
  }
}

export const signalRService = new SignalRService();
