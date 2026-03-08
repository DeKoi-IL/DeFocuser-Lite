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

#include "indi_defocuserlite.h"
#include <indicom.h>     // tty_write_string, tty_nread_section, etc.
#include <connectionplugins/connectionserial.h>

#include <cstring>
#include <cstdlib>
#include <memory>
#include <cmath>

// ── Singleton ────────────────────────────────────────────────────────────────
static std::unique_ptr<DeFocuserLite> defocuserLite(new DeFocuserLite());

// ═══════════════════════════════════════════════════════════════════════════════
//  Construction
// ═══════════════════════════════════════════════════════════════════════════════

DeFocuserLite::DeFocuserLite()
{
    setVersion(CDRIVER_VERSION_MAJOR, CDRIVER_VERSION_MINOR);

    // Declare supported capabilities
    FI::SetCapability(FOCUSER_CAN_ABS_MOVE |
                      FOCUSER_CAN_REL_MOVE |
                      FOCUSER_CAN_ABORT    |
                      FOCUSER_CAN_REVERSE  |
                      FOCUSER_CAN_SYNC);

    // Default serial connection settings: 9600 baud
    setSupportedConnections(CONNECTION_SERIAL);
}

const char *DeFocuserLite::getDefaultName()
{
    return "DeFocuser Lite";
}

// ═══════════════════════════════════════════════════════════════════════════════
//  Properties
// ═══════════════════════════════════════════════════════════════════════════════

bool DeFocuserLite::initProperties()
{
    INDI::Focuser::initProperties();

    // ── Serial defaults ──────────────────────────────────────────────────
    serialConnection->setDefaultBaudRate(Connection::Serial::B_9600);
    serialConnection->setDefaultPort("/dev/ttyACM0");

    // ── Focuser position range ───────────────────────────────────────────
    FocusAbsPosN[0].min  = 0;
    FocusAbsPosN[0].max  = 100000;
    FocusAbsPosN[0].step = 1;

    FocusRelPosN[0].min  = 0;
    FocusRelPosN[0].max  = 50000;
    FocusRelPosN[0].step = 1;

    FocusMaxPosN[0].value = 100000;

    // ── Calibration Start button ─────────────────────────────────────────
    CalibrationStartSP[0].fill("CALIBRATE", "Start Calibration", ISS_OFF);
    CalibrationStartSP.fill(getDeviceName(), "CALIBRATION_START", "Calibration",
                            MAIN_CONTROL_TAB, IP_RW, ISR_ATMOST1, 0, IPS_IDLE);

    // ── Set Limit button ─────────────────────────────────────────────────
    SetLimitSP[0].fill("SET_LIMIT", "Set Limit", ISS_OFF);
    SetLimitSP.fill(getDeviceName(), "SET_LIMIT_BTN", "Set Limit",
                    MAIN_CONTROL_TAB, IP_RW, ISR_ATMOST1, 0, IPS_IDLE);

    // ── Calibration status light ─────────────────────────────────────────
    CalibrationStatusLP[0].fill("CALIBRATING", "Calibrating", IPS_IDLE);
    CalibrationStatusLP.fill(getDeviceName(), "CALIBRATION_STATUS", "Calibration Status",
                             MAIN_CONTROL_TAB, IP_RO, 0, IPS_IDLE);

    // ── Misc ─────────────────────────────────────────────────────────────
    addDebugControl();
    addSimulationControl();

    return true;
}

bool DeFocuserLite::updateProperties()
{
    INDI::Focuser::updateProperties();

    if (isConnected())
    {
        defineProperty(CalibrationStartSP);
        defineProperty(SetLimitSP);
        defineProperty(CalibrationStatusLP);
    }
    else
    {
        deleteProperty(CalibrationStartSP);
        deleteProperty(SetLimitSP);
        deleteProperty(CalibrationStatusLP);
    }

    return true;
}

bool DeFocuserLite::saveConfigItems(FILE *fp)
{
    INDI::Focuser::saveConfigItems(fp);
    return true;
}

// ═══════════════════════════════════════════════════════════════════════════════
//  Connection / Handshake
// ═══════════════════════════════════════════════════════════════════════════════

bool DeFocuserLite::Handshake()
{
    // Send PING and verify the GUID in the response
    char response[CMD_MAX_LEN] = {0};

    if (!sendCommand("COMMAND:PING", response, sizeof(response)))
    {
        LOG_ERROR("Handshake: no response to PING.");
        return false;
    }

    // Expected: "RESULT:PING:OK:dfafe960-d19c-4abd-af4a-4dc5f49775a3"
    if (strstr(response, DEVICE_GUID) == nullptr)
    {
        LOGF_ERROR("Handshake: unexpected response: %s", response);
        return false;
    }

    LOG_INFO("DeFocuser Lite connected successfully.");

    // Read initial state from the device
    uint32_t pos = 0, maxPos = 0;
    bool reversed = false;

    if (getPosition(pos))
    {
        FocusAbsPosN[0].value = pos;
    }

    if (getMaxPosition(maxPos))
    {
        FocusAbsPosN[0].max   = maxPos;
        FocusMaxPosN[0].value = maxPos;
    }

    if (getIsReverse(reversed))
    {
        FocusReverseS[INDI_ENABLED].s  = reversed ? ISS_ON : ISS_OFF;
        FocusReverseS[INDI_DISABLED].s = reversed ? ISS_OFF : ISS_ON;
        FocusReverseSP.s = IPS_OK;
        IDSetSwitch(&FocusReverseSP, nullptr);
    }

    // Start the polling timer
    SetTimer(POLL_IDLE_MS);

    return true;
}

// ═══════════════════════════════════════════════════════════════════════════════
//  Focuser Interface
// ═══════════════════════════════════════════════════════════════════════════════

IPState DeFocuserLite::MoveAbsFocuser(uint32_t targetTicks)
{
    char cmd[CMD_MAX_LEN];
    char response[CMD_MAX_LEN] = {0};

    snprintf(cmd, sizeof(cmd), "COMMAND:FOCUSER:MOVE:%u", targetTicks);

    if (!sendCommand(cmd, response, sizeof(response)))
    {
        LOG_ERROR("MoveAbsFocuser: failed to send MOVE command.");
        return IPS_ALERT;
    }

    if (strstr(response, "OK") == nullptr)
    {
        LOGF_ERROR("MoveAbsFocuser: device returned error: %s", response);
        return IPS_ALERT;
    }

    // Switch to faster polling while moving
    SetTimer(POLL_ACTIVE_MS);

    return IPS_BUSY;
}

IPState DeFocuserLite::MoveRelFocuser(FocusDirection dir, uint32_t ticks)
{
    uint32_t currentPos = static_cast<uint32_t>(FocusAbsPosN[0].value);
    uint32_t targetPos;

    if (dir == FOCUS_INWARD)
    {
        targetPos = (ticks > currentPos) ? 0 : currentPos - ticks;
    }
    else
    {
        targetPos = currentPos + ticks;
        uint32_t maxPos = static_cast<uint32_t>(FocusAbsPosN[0].max);
        if (targetPos > maxPos)
            targetPos = maxPos;
    }

    return MoveAbsFocuser(targetPos);
}

bool DeFocuserLite::AbortFocuser()
{
    char response[CMD_MAX_LEN] = {0};

    if (!sendCommand("COMMAND:FOCUSER:HALT", response, sizeof(response)))
    {
        LOG_ERROR("AbortFocuser: failed to send HALT command.");
        return false;
    }

    if (strstr(response, "OK") == nullptr)
    {
        LOGF_ERROR("AbortFocuser: device returned error: %s", response);
        return false;
    }

    // Update position after halt
    uint32_t pos = 0;
    if (getPosition(pos))
    {
        FocusAbsPosN[0].value = pos;
        IDSetNumber(&FocusAbsPosNP, nullptr);
    }

    return true;
}

bool DeFocuserLite::SyncFocuser(uint32_t ticks)
{
    char cmd[CMD_MAX_LEN];
    char response[CMD_MAX_LEN] = {0};

    snprintf(cmd, sizeof(cmd), "COMMAND:FOCUSER:SETPOSITION:%u", ticks);

    if (!sendCommand(cmd, response, sizeof(response)))
    {
        LOG_ERROR("SyncFocuser: failed to send SETPOSITION command.");
        return false;
    }

    if (strstr(response, "OK") == nullptr)
    {
        LOGF_ERROR("SyncFocuser: device returned error: %s", response);
        return false;
    }

    FocusAbsPosN[0].value = ticks;
    IDSetNumber(&FocusAbsPosNP, nullptr);

    return true;
}

bool DeFocuserLite::ReverseFocuser(bool enabled)
{
    char cmd[CMD_MAX_LEN];
    char response[CMD_MAX_LEN] = {0};

    snprintf(cmd, sizeof(cmd), "COMMAND:FOCUSER:SETREVERSE:%s", enabled ? "TRUE" : "FALSE");

    if (!sendCommand(cmd, response, sizeof(response)))
    {
        LOG_ERROR("ReverseFocuser: failed to send SETREVERSE command.");
        return false;
    }

    if (strstr(response, "OK") == nullptr)
    {
        LOGF_ERROR("ReverseFocuser: device returned error: %s", response);
        return false;
    }

    return true;
}

// ═══════════════════════════════════════════════════════════════════════════════
//  Polling
// ═══════════════════════════════════════════════════════════════════════════════

void DeFocuserLite::TimerHit()
{
    if (!isConnected())
        return;

    // ── Poll position ────────────────────────────────────────────────────
    uint32_t pos = 0;
    if (getPosition(pos))
    {
        FocusAbsPosN[0].value = pos;
        IDSetNumber(&FocusAbsPosNP, nullptr);
    }

    // ── Poll max position (may change after calibration) ─────────────────
    uint32_t maxPos = 0;
    if (getMaxPosition(maxPos))
    {
        if (static_cast<uint32_t>(FocusMaxPosN[0].value) != maxPos)
        {
            FocusAbsPosN[0].max   = maxPos;
            FocusMaxPosN[0].value = maxPos;
            IDSetNumber(&FocusAbsPosNP, nullptr);
            IDSetNumber(&FocusMaxPosNP, nullptr);
        }
    }

    // ── Poll moving state ────────────────────────────────────────────────
    bool isMoving = false;
    if (getIsMoving(isMoving))
    {
        if (isMoving)
        {
            if (FocusAbsPosNP.s != IPS_BUSY)
            {
                FocusAbsPosNP.s = IPS_BUSY;
                IDSetNumber(&FocusAbsPosNP, nullptr);
            }
        }
        else
        {
            if (FocusAbsPosNP.s == IPS_BUSY)
            {
                FocusAbsPosNP.s = IPS_OK;
                IDSetNumber(&FocusAbsPosNP, nullptr);

                FocusRelPosNP.s = IPS_OK;
                IDSetNumber(&FocusRelPosNP, nullptr);

                LOG_INFO("Focuser reached target position.");
            }
        }
    }

    // ── Poll calibration state ───────────────────────────────────────────
    bool calibrating = false;
    if (getIsCalibrating(calibrating))
    {
        if (calibrating != m_IsCalibrating)
        {
            m_IsCalibrating = calibrating;

            if (calibrating)
            {
                CalibrationStatusLP[0].setState(IPS_BUSY);
                CalibrationStatusLP.setState(IPS_BUSY);
                CalibrationStartSP.setState(IPS_BUSY);
                LOG_INFO("Calibration in progress...");
            }
            else
            {
                CalibrationStatusLP[0].setState(IPS_OK);
                CalibrationStatusLP.setState(IPS_OK);
                CalibrationStartSP.setState(IPS_OK);
                CalibrationStartSP[0].setState(ISS_OFF);
                LOG_INFO("Calibration completed.");

                // Refresh max position after calibration
                uint32_t newMax = 0;
                if (getMaxPosition(newMax))
                {
                    FocusAbsPosN[0].max   = newMax;
                    FocusMaxPosN[0].value = newMax;
                    IDSetNumber(&FocusAbsPosNP, nullptr);
                    IDSetNumber(&FocusMaxPosNP, nullptr);
                }
            }

            CalibrationStatusLP.apply();
            CalibrationStartSP.apply();
        }
    }

    // ── Schedule next poll ───────────────────────────────────────────────
    int interval = (isMoving || m_IsCalibrating) ? POLL_ACTIVE_MS : POLL_IDLE_MS;
    SetTimer(interval);
}

// ═══════════════════════════════════════════════════════════════════════════════
//  Switch Handling (Calibration)
// ═══════════════════════════════════════════════════════════════════════════════

bool DeFocuserLite::ISNewSwitch(const char *dev, const char *name, ISState *states, char *names[], int n)
{
    if (dev != nullptr && strcmp(dev, getDeviceName()) != 0)
        return false;

    // ── Calibration Start ────────────────────────────────────────────────
    if (CalibrationStartSP.isNameMatch(name))
    {
        CalibrationStartSP.update(states, names, n);

        char response[CMD_MAX_LEN] = {0};
        if (!sendCommand("COMMAND:FOCUSER:CALIBRATE", response, sizeof(response)))
        {
            CalibrationStartSP.setState(IPS_ALERT);
            CalibrationStartSP.apply();
            LOG_ERROR("Failed to start calibration.");
            return true;
        }

        if (strstr(response, "OK") != nullptr)
        {
            m_IsCalibrating = true;
            CalibrationStartSP.setState(IPS_BUSY);
            CalibrationStatusLP[0].setState(IPS_BUSY);
            CalibrationStatusLP.setState(IPS_BUSY);
            CalibrationStatusLP.apply();
            LOG_INFO("Calibration started. The focuser will move to find its limits.");
            LOG_INFO("Press 'Set Limit' when the focuser reaches a physical limit.");
        }
        else
        {
            CalibrationStartSP.setState(IPS_ALERT);
            LOGF_ERROR("Calibration command failed: %s", response);
        }

        CalibrationStartSP.apply();
        return true;
    }

    // ── Set Limit ────────────────────────────────────────────────────────
    if (SetLimitSP.isNameMatch(name))
    {
        SetLimitSP.update(states, names, n);

        if (!m_IsCalibrating)
        {
            SetLimitSP.setState(IPS_IDLE);
            SetLimitSP[0].setState(ISS_OFF);
            SetLimitSP.apply();
            LOG_WARN("Set Limit is only available during calibration.");
            return true;
        }

        char response[CMD_MAX_LEN] = {0};
        if (!sendCommand("COMMAND:FOCUSER:SETLIMIT", response, sizeof(response)))
        {
            SetLimitSP.setState(IPS_ALERT);
            SetLimitSP.apply();
            LOG_ERROR("Failed to send SETLIMIT command.");
            return true;
        }

        if (strstr(response, "OK") != nullptr)
        {
            SetLimitSP.setState(IPS_OK);
            LOG_INFO("Limit set. Calibration advancing to next step...");
        }
        else
        {
            SetLimitSP.setState(IPS_ALERT);
            LOGF_ERROR("SETLIMIT command failed: %s", response);
        }

        SetLimitSP[0].setState(ISS_OFF);
        SetLimitSP.apply();
        return true;
    }

    return INDI::Focuser::ISNewSwitch(dev, name, states, names, n);
}

// ═══════════════════════════════════════════════════════════════════════════════
//  Serial Helpers
// ═══════════════════════════════════════════════════════════════════════════════

bool DeFocuserLite::sendCommand(const char *cmd, char *response, size_t responseLen)
{
    int portFD = PortFD;
    int nbytes_written = 0, nbytes_read = 0;
    int rc;

    // Flush any stale data
    tcflush(portFD, TCIOFLUSH);

    // Build the command string with newline terminator
    char cmdStr[CMD_MAX_LEN];
    snprintf(cmdStr, sizeof(cmdStr), "%s\n", cmd);

    LOGF_DEBUG("CMD: %s", cmd);

    // Write command
    rc = tty_write_string(portFD, cmdStr, &nbytes_written);
    if (rc != TTY_OK)
    {
        char errMsg[MAXRBUF];
        tty_error_msg(rc, errMsg, MAXRBUF);
        LOGF_ERROR("Serial write error: %s", errMsg);
        return false;
    }

    // Read response (newline-terminated)
    rc = tty_nread_section(portFD, response, responseLen, '\n', CMD_TIMEOUT_MS, &nbytes_read);
    if (rc != TTY_OK)
    {
        char errMsg[MAXRBUF];
        tty_error_msg(rc, errMsg, MAXRBUF);
        LOGF_ERROR("Serial read error: %s", errMsg);
        return false;
    }

    // Strip trailing newline/carriage return
    response[nbytes_read] = '\0';
    char *nl = strchr(response, '\r');
    if (nl) *nl = '\0';
    nl = strchr(response, '\n');
    if (nl) *nl = '\0';

    LOGF_DEBUG("RES: %s", response);

    return true;
}

bool DeFocuserLite::sendCommandOnly(const char *cmd)
{
    int portFD = PortFD;
    int nbytes_written = 0;

    char cmdStr[CMD_MAX_LEN];
    snprintf(cmdStr, sizeof(cmdStr), "%s\n", cmd);

    LOGF_DEBUG("CMD (no-wait): %s", cmd);

    int rc = tty_write_string(portFD, cmdStr, &nbytes_written);
    if (rc != TTY_OK)
    {
        char errMsg[MAXRBUF];
        tty_error_msg(rc, errMsg, MAXRBUF);
        LOGF_ERROR("Serial write error: %s", errMsg);
        return false;
    }

    return true;
}

bool DeFocuserLite::readResponse(char *response, size_t responseLen)
{
    int portFD = PortFD;
    int nbytes_read = 0;

    int rc = tty_nread_section(portFD, response, responseLen, '\n', CMD_TIMEOUT_MS, &nbytes_read);
    if (rc != TTY_OK)
    {
        char errMsg[MAXRBUF];
        tty_error_msg(rc, errMsg, MAXRBUF);
        LOGF_ERROR("Serial read error: %s", errMsg);
        return false;
    }

    response[nbytes_read] = '\0';
    char *nl = strchr(response, '\r');
    if (nl) *nl = '\0';
    nl = strchr(response, '\n');
    if (nl) *nl = '\0';

    LOGF_DEBUG("RES: %s", response);

    return true;
}

// ═══════════════════════════════════════════════════════════════════════════════
//  Device Query Helpers
// ═══════════════════════════════════════════════════════════════════════════════

bool DeFocuserLite::getPosition(uint32_t &position)
{
    char response[CMD_MAX_LEN] = {0};

    if (!sendCommand("COMMAND:FOCUSER:GETPOSITION", response, sizeof(response)))
        return false;

    // Expected: "RESULT:FOCUSER:POSITION:12345"
    const char *prefix = "RESULT:FOCUSER:POSITION:";
    char *value = strstr(response, prefix);
    if (value == nullptr)
        return false;

    value += strlen(prefix);
    position = static_cast<uint32_t>(atol(value));
    return true;
}

bool DeFocuserLite::getMaxPosition(uint32_t &maxPosition)
{
    char response[CMD_MAX_LEN] = {0};

    if (!sendCommand("COMMAND:FOCUSER:GETMAXPOSITION", response, sizeof(response)))
        return false;

    // Expected: "RESULT:FOCUSER:MAXPOSITION:100000"
    const char *prefix = "RESULT:FOCUSER:MAXPOSITION:";
    char *value = strstr(response, prefix);
    if (value == nullptr)
        return false;

    value += strlen(prefix);
    maxPosition = static_cast<uint32_t>(atol(value));
    return true;
}

bool DeFocuserLite::getIsMoving(bool &moving)
{
    char response[CMD_MAX_LEN] = {0};

    if (!sendCommand("COMMAND:FOCUSER:ISMOVING", response, sizeof(response)))
        return false;

    // Expected: "RESULT:FOCUSER:ISMOVING:TRUE" or "...FALSE"
    moving = (strstr(response, "TRUE") != nullptr);
    return true;
}

bool DeFocuserLite::getIsCalibrating(bool &calibrating)
{
    char response[CMD_MAX_LEN] = {0};

    if (!sendCommand("COMMAND:FOCUSER:ISCALIBRATING", response, sizeof(response)))
        return false;

    // Expected: "RESULT:FOCUSER:ISCALIBRATING:TRUE" or "...FALSE"
    calibrating = (strstr(response, "TRUE") != nullptr);
    return true;
}

bool DeFocuserLite::getIsReverse(bool &reversed)
{
    char response[CMD_MAX_LEN] = {0};

    if (!sendCommand("COMMAND:FOCUSER:ISREVERSE", response, sizeof(response)))
        return false;

    // Expected: "RESULT:FOCUSER:ISREVERSE:TRUE" or "...FALSE"
    reversed = (strstr(response, "TRUE") != nullptr);
    return true;
}
