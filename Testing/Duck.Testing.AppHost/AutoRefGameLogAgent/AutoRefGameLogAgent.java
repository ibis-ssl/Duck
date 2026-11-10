package edu.ibis.duck.autoref;

import java.lang.instrument.Instrumentation;
import java.lang.reflect.Field;
import java.lang.reflect.Method;
import java.util.Optional;

/** Starts AutoRef's existing GameLogRecorder after its modules become active. */
public final class AutoRefGameLogAgent {
    private static final long STARTUP_TIMEOUT_MILLIS = 180_000;
    private static final long POLL_INTERVAL_MILLIS = 250;

    private AutoRefGameLogAgent() { }

    public static void premain(String ignored, Instrumentation instrumentation) {
        Thread starter = new Thread(AutoRefGameLogAgent::startRecorder, "duck-autoref-game-log-startup");
        starter.setDaemon(true);
        starter.start();
    }

    private static void startRecorder() {
        long deadline = System.currentTimeMillis() + STARTUP_TIMEOUT_MILLIS;
        Throwable lastFailure = null;
        String lastObservedModulesState = "unobserved";
        String lastObservedRecorderModuleAvailability = "not-checked";

        while (System.currentTimeMillis() < deadline) {
            lastObservedRecorderModuleAvailability = "not-checked";
            try {
                Class<?> modelType = Class.forName("edu.tigers.sumatra.model.SumatraModel");
                Object model = modelType.getMethod("getInstance").invoke(null);
                Object modulesState = modelType.getMethod("getModulesState").invoke(model);
                Object state = modulesState.getClass().getMethod("get").invoke(modulesState);
                lastObservedModulesState = String.valueOf(state);
                if (!"ACTIVE".equals(lastObservedModulesState)) {
                    Thread.sleep(POLL_INTERVAL_MILLIS);
                    continue;
                }

                Class<?> recorderType = Class.forName("edu.tigers.sumatra.gamelog.GameLogRecorder");
                Method getModule = modelType.getMethod("getModuleOpt", Class.class);
                Object optional = getModule.invoke(model, recorderType);
                boolean recorderAvailable = optional instanceof Optional<?> recorderOptional
                        && recorderOptional.isPresent();
                lastObservedRecorderModuleAvailability = Boolean.toString(recorderAvailable);
                if (!recorderAvailable) {
                    Thread.sleep(POLL_INTERVAL_MILLIS);
                    continue;
                }

                Optional<?> recorderOptional = (Optional<?>) optional;
                Object recorder = recorderOptional.get();
                recorderType.getMethod("setMatchInfo", String.class, String.class, String.class)
                        .invoke(recorder, "MATCH", "YELLOW", "BLUE");
                recorderType.getMethod("setRecording", boolean.class).invoke(recorder, true);

                Field writerField = recorderType.getDeclaredField("writer");
                writerField.setAccessible(true);
                Object writer = writerField.get(recorder);
                boolean open = (boolean) writer.getClass().getMethod("isOpen").invoke(writer);
                if (!open) {
                    throw new IllegalStateException("AutoRef GameLogWriter did not open its output file.");
                }

                System.out.println("DUCK_AUTOREF_GAMELOG_STARTED=true");
                return;
            } catch (InterruptedException interrupted) {
                Thread.currentThread().interrupt();
                System.err.println("DUCK_AUTOREF_GAMELOG_STARTED=false reason=interrupted");
                return;
            } catch (Throwable failure) {
                lastFailure = failure;
                try {
                    Thread.sleep(POLL_INTERVAL_MILLIS);
                } catch (InterruptedException interrupted) {
                    Thread.currentThread().interrupt();
                    System.err.println("DUCK_AUTOREF_GAMELOG_STARTED=false reason=interrupted");
                    return;
                }
            }
        }

        System.err.println("DUCK_AUTOREF_GAMELOG_STARTED=false reason=startup-timeout");
        System.err.println("DUCK_AUTOREF_GAMELOG_DIAGNOSTIC modulesState=" + lastObservedModulesState
                + " recorderModuleAvailable=" + lastObservedRecorderModuleAvailability);
        if (lastFailure != null) {
            lastFailure.printStackTrace(System.err);
        }
    }
}
