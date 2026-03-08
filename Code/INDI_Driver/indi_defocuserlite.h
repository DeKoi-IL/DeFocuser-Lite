/*
    DeFocuser Lite INDI Focuser Driver
    Copyright (C) 2024 DeKoi

    This driver communicates with the DeFocuser Lite ESP32-based
    focuser controller over USB CDC serial at 9600 baud using a
    text-based COMMAND:/RESULT: protocol.

    This library is free software; you can redistribute it and/or
    modify it under the terms of the GNU Lesser General Public
    License as published by the Free Software Foundation; either
    version 2.1 of the License, or (at your option) any later version.

    This library is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
    Lesser General Public License for more details.
*/

#pragma once

#include "config.h"
#include <indifocuser.h>

class DeFocuserLite : public INDI::Focuser
{
  public:
    DeFocuserLite();
    virtual ~DeFocuserLite() override = default;

    // ── INDI overrides ────────────────────────────────────────────────
    const char *getDefaultName() override;
    bool initProperties() override;
    bool updateProperties() override;
    bool saveConfigItems(FILE *fp) override;

    // ── Connection ────────────────────────────────────────────────────
    bool Handshake() override;

    // ── Focuser interface ─────────────────────────────────────────────
    IPState MoveAbsFocuser(uint32_t targetTicks) override;
    IPState MoveRelFocuser(FocusDirection dir, uint32_t ticks) override;
    bool AbortFocuser() override;
    bool SyncFocuser(uint32_t ticks) override;
    bool ReverseFocuser(bool enabled) override;

    // ── Polling ───────────────────────────────────────────────────────
    void TimerHit() override;

    // ── Switch handling (calibration) ─────────────────────────────────
    bool ISNewSwitch(const char *dev, const char *name, ISState *states, char *names[], int n) override;

  private:
    // ── Serial helpers ────────────────────────────────────────────────
    /// Send a command and wait for a single-line response.
    bool sendCommand(const char *cmd, char *response, size_t responseLen);
    /// Send a command without reading a response (fire-and-forget).
    bool sendCommandOnly(const char *cmd);
    /// Read a single newline-terminated line from the serial port.
    bool readResponse(char *response, size_t responseLen);

    // ── Device query helpers ──────────────────────────────────────────
    bool getPosition(uint32_t &position);
    bool getMaxPosition(uint32_t &maxPosition);
    bool getIsMoving(bool &moving);
    bool getIsCalibrating(bool &calibrating);
    bool getIsReverse(bool &reversed);

    // ── Custom properties ─────────────────────────────────────────────
    // Calibration Start button
    INDI::PropertySwitch CalibrationStartSP {1};

    // Set Limit button (only active during calibration)
    INDI::PropertySwitch SetLimitSP {1};

    // Calibration status light
    INDI::PropertyLight CalibrationStatusLP {1};

    // ── State ─────────────────────────────────────────────────────────
    bool m_IsCalibrating {false};

    // ── Constants ─────────────────────────────────────────────────────
    static constexpr const char *DEVICE_GUID = "dfafe960-d19c-4abd-af4a-4dc5f49775a3";
    static constexpr int CMD_TIMEOUT_MS      = 2000;  // Serial response timeout
    static constexpr int POLL_ACTIVE_MS      = 500;   // Polling interval while moving
    static constexpr int POLL_IDLE_MS        = 1000;  // Polling interval while idle
    static constexpr int CMD_MAX_LEN         = 128;   // Max command/response string length
};
